using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.Waiter;
using ScanNow.Application.Mappers;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.Order
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<PlaceOrderRequest> _placeOrderValidator;
        private readonly IOrderUpdatePublisher _publisher;

        public OrderService(
            IOrderRepository repository,
            IUnitOfWork unitOfWork,
            IValidator<PlaceOrderRequest> placeOrderValidator,
            IOrderUpdatePublisher publisher)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _placeOrderValidator = placeOrderValidator;
            _publisher = publisher;
        }

        public async Task<CustomerOrderResponse> PlaceOrderAsync(string sessionCode, PlaceOrderRequest request)
        {
            await _placeOrderValidator.ValidateAndThrowAsync(request);

            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var session = await _repository.GetActiveSessionByCodeAsync(normalizedCode)
                ?? throw new NotFoundException("Session not found or expired");

            var branch = session.Branch;

            var orderItems = await BuildOrderItemsAsync(request, session.BranchId);

            var subTotal = orderItems.Sum(x => x.SubTotal);
            var vatPercent = branch.VatPercent;
            var vatAmount = Math.Round(subTotal * vatPercent / 100, 2);
            var serviceChargePercent = branch.ServiceChargePercent;
            var serviceChargeAmount = Math.Round(subTotal * serviceChargePercent / 100, 2);
            var totalAmount = subTotal + vatAmount + serviceChargeAmount;

            Domain.Entities.Order order;

            if (session.ActiveOrderId.HasValue)
            {
                order = await _repository.GetActiveOrderByIdAsync(session.ActiveOrderId.Value)
                    ?? throw new NotFoundException("Active order not found");

                await _repository.MarkPendingPaymentsFailedAsync(order.Id, DateTime.UtcNow);

                foreach (var item in orderItems)
                {
                    item.OrderId = order.Id;
                    order.Items.Add(item);
                }

                order.SubTotal += subTotal;
                order.VatAmount += vatAmount;
                order.ServiceChargeAmount += serviceChargeAmount;
                order.TotalAmount += totalAmount;
                order.Status = WaiterService.CalculateOrderStatus(order.Items.ToList());
                order.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var orderNumber = GenerateOrderNumber();
                order = new Domain.Entities.Order
                {
                    Id = Guid.NewGuid(),
                    BranchId = session.BranchId,
                    TableId = session.TableId,
                    OrderNumber = orderNumber,
                    CustomerName = request.CustomerName?.Trim(),
                    CustomerPhone = request.CustomerPhone?.Trim(),
                    CustomerNote = request.CustomerNote?.Trim(),
                    SubTotal = subTotal,
                    VatPercent = vatPercent,
                    VatAmount = vatAmount,
                    ServiceChargePercent = serviceChargePercent,
                    ServiceChargeAmount = serviceChargeAmount,
                    DiscountAmount = 0,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.PendingConfirmation,
                    OrderSource = OrderSource.QR,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Items = orderItems
                };

                await _repository.AddOrderAsync(order);

                session.ActiveOrderId = order.Id;
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();

            var response = CustomerOrderMapper.Map(order);
            await _publisher.PublishOrderUpdatedAsync(response);
            return response;
        }

        public async Task<CustomerOrderResponse> GetPublicOrderDetailAsync(string sessionCode, Guid orderId)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var order = await _repository.GetActiveSessionOrderAsync(normalizedCode, orderId)
                ?? throw new NotFoundException("Order not found");

            return CustomerOrderMapper.Map(order);
        }

        public async Task<OrderInvoiceListResponse> GetBranchOrdersAsync(Guid branchId, OrderInvoiceQuery query)
        {
            var orders = await _repository.GetOrdersByBranchAsync(branchId);
            var filteredOrders = ApplyInvoiceFilters(orders, query).ToList();
            var sortedOrders = ApplyInvoiceSort(filteredOrders, query).ToList();
            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var pageItems = sortedOrders
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapTableOrderHistory)
                .ToList();

            return new OrderInvoiceListResponse
            {
                Orders = new ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableOrderHistoryResponse>
                {
                    Items = pageItems,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalItems = filteredOrders.Count
                },
                TotalOrders = filteredOrders.Count,
                TotalAmount = filteredOrders.Sum(x => x.TotalAmount),
                PaidAmount = filteredOrders
                    .Where(x => GetLatestPayment(x)?.Status == PaymentStatus.SUCCESS)
                    .Sum(x => x.TotalAmount),
                PendingAmount = filteredOrders
                    .Where(x => GetLatestPayment(x)?.Status == PaymentStatus.PENDING)
                    .Sum(x => x.TotalAmount),
                RefundedAmount = filteredOrders
                    .Where(x => GetLatestPayment(x)?.Status == PaymentStatus.REFUNDED)
                    .Sum(x => x.TotalAmount)
            };
        }

        public async Task<List<TableOrderHistoryResponse>> GetBranchTableOrderHistoryAsync(Guid branchId, Guid tableId)
        {
            var orders = await _repository.GetOrdersByBranchTableAsync(branchId, tableId);
            return orders.Select(MapTableOrderHistory).ToList();
        }

        public async Task<List<TableOrderHistoryResponse>> GetActiveBranchTableOrdersAsync(Guid branchId, Guid tableId)
        {
            var orders = await _repository.GetActiveSessionOrdersByBranchTableAsync(branchId, tableId);
            return orders.Select(MapTableOrderHistory).ToList();
        }

        public async Task CancelOrderAsync(Guid orderId, Guid branchId)
        {
            var order = await _repository.GetActiveOrderByIdAsync(orderId)
                ?? throw new NotFoundException("Order not found");

            if (order.BranchId != branchId)
                throw new ForbiddenException("You do not have permission to cancel this order");

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
                throw new BusinessRuleException("Order is already cancelled or completed");

            var hasInProgressItems = order.Items.Any(x =>
                x.Status == OrderItemStatus.Cooking ||
                x.Status == OrderItemStatus.Ready ||
                x.Status == OrderItemStatus.Served);

            if (hasInProgressItems)
                throw new BusinessRuleException("Cannot cancel order: some items are already being prepared or served");

            foreach (var item in order.Items.Where(x => x.Status != OrderItemStatus.Cancelled))
            {
                item.Status = OrderItemStatus.Cancelled;
                item.CancelledAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;
            }

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();

            await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));
        }

        private async Task<List<OrderItem>> BuildOrderItemsAsync(PlaceOrderRequest request, Guid branchId)
        {
            var result = new List<OrderItem>();

            foreach (var itemRequest in request.Items)
            {
                var menuItem = await _repository.GetMenuItemByIdAsync(itemRequest.MenuItemId)
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

        internal static string GenerateOrderNumber()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"ORD-{timestamp}-{suffix}";
        }

        internal static OrderResponse MapOrder(Domain.Entities.Order order)
        {
            return new OrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                BranchId = order.BranchId,
                TableId = order.TableId,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerNote = order.CustomerNote,
                SubTotal = order.SubTotal,
                VatPercent = order.VatPercent,
                VatAmount = order.VatAmount,
                ServiceChargePercent = order.ServiceChargePercent,
                ServiceChargeAmount = order.ServiceChargeAmount,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                OrderSource = order.OrderSource,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(MapOrderItem).ToList()
            };
        }

        private static OrderItemResponse MapOrderItem(OrderItem item)
        {
            return new OrderItemResponse
            {
                OrderItemId = item.Id,
                MenuItemId = item.MenuItemId,
                MenuItemName = item.MenuItemName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                SubTotal = item.SubTotal,
                Note = item.Note,
                Status = item.Status,
                EstimatedCookingMinutes = item.EstimatedCookingMinutes
            };
        }

        private static IEnumerable<Domain.Entities.Order> ApplyInvoiceFilters(IEnumerable<Domain.Entities.Order> orders, OrderInvoiceQuery query)
        {
            if (query.TableId.HasValue)
            {
                orders = orders.Where(x => x.TableId == query.TableId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.TableNumber))
            {
                var tableNumber = query.TableNumber.Trim();
                orders = orders.Where(x => x.Table?.TableNumber.Contains(tableNumber, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (query.Status.HasValue)
            {
                orders = orders.Where(x => x.Status == query.Status.Value);
            }

            if (query.PaymentMethod.HasValue)
            {
                orders = orders.Where(x => GetLatestPayment(x)?.Method == query.PaymentMethod.Value);
            }

            if (query.PaymentStatus.HasValue)
            {
                orders = orders.Where(x => GetLatestPayment(x)?.Status == query.PaymentStatus.Value);
            }

            if (query.FromDate.HasValue)
            {
                orders = orders.Where(x => x.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                var inclusiveTo = query.ToDate.Value.Date.AddDays(1);
                orders = orders.Where(x => x.CreatedAt < inclusiveTo);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                orders = orders.Where(x =>
                    x.OrderNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Table?.TableNumber.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || (x.CustomerName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || (x.CustomerPhone?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || x.QrSessions.Any(session => session.SessionToken.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            return orders;
        }

        private static IEnumerable<Domain.Entities.Order> ApplyInvoiceSort(IEnumerable<Domain.Entities.Order> orders, OrderInvoiceQuery query)
        {
            var desc = query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
            return (query.SortBy ?? "createdAt").Trim().ToLowerInvariant() switch
            {
                "ordernumber" => desc ? orders.OrderByDescending(x => x.OrderNumber) : orders.OrderBy(x => x.OrderNumber),
                "tablenumber" => desc ? orders.OrderByDescending(x => x.Table?.TableNumber) : orders.OrderBy(x => x.Table?.TableNumber),
                "totalamount" => desc ? orders.OrderByDescending(x => x.TotalAmount) : orders.OrderBy(x => x.TotalAmount),
                "status" => desc ? orders.OrderByDescending(x => x.Status) : orders.OrderBy(x => x.Status),
                "paymentstatus" => desc ? orders.OrderByDescending(x => GetLatestPayment(x)?.Status) : orders.OrderBy(x => GetLatestPayment(x)?.Status),
                "paidat" => desc ? orders.OrderByDescending(x => GetLatestPayment(x)?.PaidAt) : orders.OrderBy(x => GetLatestPayment(x)?.PaidAt),
                _ => desc ? orders.OrderByDescending(x => x.CreatedAt) : orders.OrderBy(x => x.CreatedAt)
            };
        }

        private static Payment? GetLatestPayment(Domain.Entities.Order order)
        {
            return order.Payments
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefault();
        }

        private static TableOrderHistoryResponse MapTableOrderHistory(Domain.Entities.Order order)
        {
            var latestPayment = GetLatestPayment(order);

            return new TableOrderHistoryResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                BranchId = order.BranchId,
                TableId = order.TableId,
                TableNumber = order.Table?.TableNumber,
                SessionCode = order.QrSessions
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault()
                    ?.SessionToken,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerNote = order.CustomerNote,
                SubTotal = order.SubTotal,
                VatAmount = order.VatAmount,
                ServiceChargeAmount = order.ServiceChargeAmount,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                PaymentMethod = latestPayment?.Method.ToString(),
                PaymentStatus = latestPayment?.Status.ToString(),
                AmountReceived = latestPayment?.AmountReceived,
                ChangeAmount = latestPayment?.ChangeAmount,
                PaidAt = latestPayment?.PaidAt,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Items = order.Items
                    .OrderBy(x => x.CreatedAt)
                    .Select(item => new CustomerOrderItemResponse
                    {
                        OrderItemId = item.Id,
                        MenuItemId = item.MenuItemId,
                        MenuItemName = item.MenuItemName,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        SubTotal = item.SubTotal,
                        Note = item.Note,
                        Status = item.Status,
                        EstimatedCookingMinutes = item.EstimatedCookingMinutes
                    })
                    .ToList()
            };
        }
    }
}
