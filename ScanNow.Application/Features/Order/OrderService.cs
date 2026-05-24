using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Order.DTOs;
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

                if (order.Status != OrderStatus.PendingConfirmation)
                    throw new BusinessRuleException("Cannot add items after an order has been confirmed");

                foreach (var item in orderItems)
                {
                    item.OrderId = order.Id;
                    order.Items.Add(item);
                }

                order.SubTotal += subTotal;
                order.VatAmount += vatAmount;
                order.ServiceChargeAmount += serviceChargeAmount;
                order.TotalAmount += totalAmount;
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
    }
}
