using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Waiter.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.Waiter
{
    public class WaiterService : IWaiterService
    {
        private readonly IWaiterRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderUpdatePublisher _publisher;

        public WaiterService(
            IWaiterRepository repository,
            IUnitOfWork unitOfWork,
            IOrderUpdatePublisher publisher)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _publisher = publisher;
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

            // Validate branch isolation
            var invalidBranchItems = items.Where(x => x.Order.BranchId != branchId).ToList();
            if (invalidBranchItems.Any())
                throw new ForbiddenException("You do not have permission to update these order items");

            // Validate status
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

            // Recalculate order statuses
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
                    Status = i.Status
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

            if (activeItems.Any(x => x.Status == OrderItemStatus.Cooking))
                return OrderStatus.Preparing;

            if (activeItems.All(x => x.Status == OrderItemStatus.Confirmed))
                return OrderStatus.Confirmed;

            return OrderStatus.PendingConfirmation;
        }
    }
}
