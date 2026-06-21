using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.Waiter.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.Waiter
{
    public class WaiterService : IWaiterService
    {
        private readonly IWaiterRepository _repository;
        private readonly IOrderRepository _orderRepository;
        private readonly ITableQrRepository _tableQrRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderUpdatePublisher _publisher;
        private readonly IValidator<CreateWaiterOrderRequest> _createOrderValidator;

        public WaiterService(
            IWaiterRepository repository,
            IOrderRepository orderRepository,
            ITableQrRepository tableQrRepository,
            IUnitOfWork unitOfWork,
            IOrderUpdatePublisher publisher,
            IValidator<CreateWaiterOrderRequest> createOrderValidator)
        {
            _repository = repository;
            _orderRepository = orderRepository;
            _tableQrRepository = tableQrRepository;
            _unitOfWork = unitOfWork;
            _publisher = publisher;
            _createOrderValidator = createOrderValidator;
        }

        public async Task<List<PendingOrderResponse>> GetPendingConfirmationOrdersAsync(Guid branchId)
        {
            var orders = await _repository.GetPendingConfirmationOrdersAsync(branchId);
            return orders.Select(MapPendingOrder).ToList();
        }

        public async Task<ConfirmOrderResponse> ConfirmOrderAsync(Guid orderId, Guid branchId)
        {
            var order = await _repository.GetOrderWithItemsAsync(orderId)
                ?? throw new NotFoundException("Order not found");

            if (order.BranchId != branchId)
                throw new ForbiddenException("You do not have permission to confirm this order");

            if (order.Status != OrderStatus.PendingConfirmation)
                throw new BusinessRuleException($"Order cannot be confirmed. Current status: {order.Status}");

            var now = DateTime.UtcNow;

            var itemsToConfirm = order.Items
                .Where(x => x.Status == OrderItemStatus.Pending)
                .ToList();

            foreach (var item in itemsToConfirm)
            {
                item.Status = OrderItemStatus.Confirmed;
                item.ConfirmedAt = now;
                item.UpdatedAt = now;
            }

            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAt = now;
            order.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync();
            await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));

            return new ConfirmOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                ConfirmedAt = order.ConfirmedAt,
                ItemsConfirmed = itemsToConfirm.Count
            };
        }

        public async Task<List<ReadyToServeTableGroup>> GetReadyToServeItemsAsync(Guid branchId)
        {
            var items = await _repository.GetReadyToServeItemsAsync(branchId);

            var result = items
                .GroupBy(x => new { x.Order.TableId, TableNumber = x.Order.Table?.TableNumber })
                .Select(tableGroup => new ReadyToServeTableGroup
                {
                    TableId = tableGroup.Key.TableId,
                    TableNumber = tableGroup.Key.TableNumber,
                    Orders = tableGroup
                        .GroupBy(x => new { x.OrderId, x.Order.OrderNumber })
                        .Select(orderGroup => new ReadyToServeOrderGroup
                        {
                            OrderId = orderGroup.Key.OrderId,
                            OrderNumber = orderGroup.Key.OrderNumber,
                            Items = orderGroup.Select(i => new ReadyToServeItemResponse
                            {
                                OrderItemId = i.Id,
                                MenuItemId = i.MenuItemId,
                                MenuItemName = i.MenuItemName,
                                Quantity = i.Quantity,
                                Note = i.Note,
                                ReadyAt = i.ReadyAt
                            }).ToList()
                        }).ToList()
                }).ToList();

            return result;
        }

        public async Task<MarkItemsServedResponse> MarkItemsServedAsync(MarkItemsServedRequest request, Guid branchId)
        {
            if (!request.OrderItemIds.Any())
                throw new Domain.Exceptions.ValidationException("OrderItemIds cannot be empty");

            var items = await _repository.GetOrderItemsByIdsAsync(request.OrderItemIds);

            if (items.Count != request.OrderItemIds.Count)
                throw new NotFoundException("One or more order items not found");

            var invalidBranchItems = items.Where(x => x.Order.BranchId != branchId).ToList();
            if (invalidBranchItems.Any())
                throw new ForbiddenException("You do not have permission to update these order items");

            var nonReadyItems = items.Where(x => x.Status != OrderItemStatus.Ready).ToList();
            if (nonReadyItems.Any())
                throw new BusinessRuleException("Only Ready items can be marked as Served");

            var now = DateTime.UtcNow;
            var affectedOrderIds = new HashSet<Guid>();

            foreach (var item in items)
            {
                item.Status = OrderItemStatus.Served;
                item.ServedAt = now;
                item.UpdatedAt = now;
                affectedOrderIds.Add(item.OrderId);
            }

            var affectedOrders = await _repository.GetOrdersByIdsAsync(affectedOrderIds.ToList());

            foreach (var order in affectedOrders)
            {
                order.Status = CalculateOrderStatus(order.Items.ToList());
                order.UpdatedAt = now;
            }

            await _unitOfWork.SaveChangesAsync();
            foreach (var order in affectedOrders)
            {
                await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));
            }

            return new MarkItemsServedResponse
            {
                ItemsServed = items.Count,
                AffectedOrderIds = affectedOrderIds.ToList()
            };
        }

        public async Task<CustomerOrderResponse> CreateWaiterOrderAsync(Guid branchId, CreateWaiterOrderRequest request)
        {
            await _createOrderValidator.ValidateAndThrowAsync(request);

            var table = await _tableQrRepository.GetTableByIdAsync(request.TableId)
                ?? throw new NotFoundException("Table not found");

            if (table.BranchId != branchId)
                throw new ForbiddenException("Table does not belong to this branch");

            if (!table.IsActive)
                throw new BusinessRuleException("Table is not active");

            var branch = await _tableQrRepository.GetBranchByIdAsync(branchId)
                ?? throw new NotFoundException("Branch not found");

            var orderItems = await BuildOrderItemsAsync(request.Items, branchId);

            var subTotal = orderItems.Sum(x => x.SubTotal);
            var vatPercent = branch.VatPercent;
            var vatAmount = Math.Round(subTotal * vatPercent / 100, 2);
            var serviceChargePercent = branch.ServiceChargePercent;
            var serviceChargeAmount = Math.Round(subTotal * serviceChargePercent / 100, 2);
            var totalAmount = subTotal + vatAmount + serviceChargeAmount;

            Domain.Entities.Order? order = null;

            // Check if there's an active session/order for this table to append to
            var activeOrders = await _orderRepository.GetActiveSessionOrdersByBranchTableAsync(branchId, request.TableId);
            var activeOrder = activeOrders.FirstOrDefault(o => o.Status == OrderStatus.PendingConfirmation
                || o.Status == OrderStatus.Confirmed
                || o.Status == OrderStatus.Preparing);

            if (activeOrder != null)
            {
                order = activeOrder;

                foreach (var item in orderItems)
                {
                    item.OrderId = order.Id;
                    order.Items.Add(item);
                }

                order.SubTotal += subTotal;
                order.VatAmount += vatAmount;
                order.ServiceChargeAmount += serviceChargeAmount;
                order.TotalAmount += totalAmount;
                order.Status = CalculateOrderStatus(order.Items.ToList());
                order.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var orderNumber = GenerateOrderNumber();
                order = new Domain.Entities.Order
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    TableId = request.TableId,
                    OrderNumber = orderNumber,
                    CustomerName = request.CustomerName?.Trim(),
                    CustomerNote = request.CustomerNote?.Trim(),
                    SubTotal = subTotal,
                    VatPercent = vatPercent,
                    VatAmount = vatAmount,
                    ServiceChargePercent = serviceChargePercent,
                    ServiceChargeAmount = serviceChargeAmount,
                    DiscountAmount = 0,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.PendingConfirmation,
                    OrderSource = OrderSource.STAFF_MANUAL,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Items = orderItems
                };

                await _orderRepository.AddOrderAsync(order);
            }

            await _unitOfWork.SaveChangesAsync();

            var response = CustomerOrderMapper.Map(order);
            await _publisher.PublishOrderUpdatedAsync(response);
            return response;
        }

        private async Task<List<OrderItem>> BuildOrderItemsAsync(List<CreateWaiterOrderItemRequest> items, Guid branchId)
        {
            var result = new List<OrderItem>();

            foreach (var itemRequest in items)
            {
                var menuItem = await _orderRepository.GetMenuItemByIdAsync(itemRequest.MenuItemId)
                    ?? throw new NotFoundException($"Menu item '{itemRequest.MenuItemId}' not found");

                if (!menuItem.IsActive)
                    throw new BusinessRuleException($"Menu item '{menuItem.Name}' is not active");

                if (!menuItem.IsAvailable)
                    throw new BusinessRuleException($"Menu item '{menuItem.Name}' is currently unavailable");

                if (menuItem.BranchId != branchId)
                    throw new BusinessRuleException($"Menu item '{menuItem.Name}' does not belong to this branch");

                var quantity = itemRequest.Quantity;
                var subTotal = menuItem.Price * quantity;

                result.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = menuItem.Id,
                    MenuItemName = menuItem.Name,
                    UnitPrice = menuItem.Price,
                    Quantity = quantity,
                    SubTotal = subTotal,
                    Note = itemRequest.Note?.Trim(),
                    EstimatedCookingMinutes = menuItem.PreparationTime,
                    Status = OrderItemStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return result;
        }

        private static string GenerateOrderNumber()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"ORD-{timestamp}-{suffix}";
        }

        private static PendingOrderResponse MapPendingOrder(Domain.Entities.Order order)
        {
            return new PendingOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                BranchId = order.BranchId,
                TableId = order.TableId,
                TableNumber = order.Table?.TableNumber,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerNote = order.CustomerNote,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(i => new PendingOrderItemResponse
                {
                    OrderItemId = i.Id,
                    MenuItemId = i.MenuItemId,
                    MenuItemName = i.MenuItemName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    SubTotal = i.SubTotal,
                    Note = i.Note,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt
                }).ToList()
            };
        }

        internal static OrderStatus CalculateOrderStatus(List<Domain.Entities.OrderItem> items)
        {
            var activeItems = items.Where(x => x.Status != OrderItemStatus.Cancelled).ToList();

            if (!activeItems.Any())
                return OrderStatus.Cancelled;

            if (activeItems.All(x => x.Status == OrderItemStatus.Served))
                return OrderStatus.Served;

            if (activeItems.Any(x => x.Status == OrderItemStatus.Served))
                return OrderStatus.PartiallyServed;

            if (activeItems.All(x => x.Status == OrderItemStatus.Ready))
                return OrderStatus.ReadyToServe;

            if (activeItems.Any(x => x.Status == OrderItemStatus.Ready))
                return OrderStatus.PartiallyReady;

            if (activeItems.All(x => x.Status == OrderItemStatus.Confirmed || x.Status == OrderItemStatus.Cooking))
                return OrderStatus.Confirmed;

            return OrderStatus.PendingConfirmation;
        }
    }
}
