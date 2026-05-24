using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Order.DTOs;
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

        public OrderService(
            IOrderRepository repository,
            IUnitOfWork unitOfWork,
            IValidator<PlaceOrderRequest> placeOrderValidator)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _placeOrderValidator = placeOrderValidator;
        }

        public async Task<OrderResponse> PlaceOrderAsync(string sessionCode, PlaceOrderRequest request)
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
                    Status = OrderStatus.PENDING,
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

            return MapOrder(order);
        }

        private async Task<List<OrderItem>> BuildOrderItemsAsync(PlaceOrderRequest request, Guid branchId)
        {
            var result = new List<OrderItem>();

            foreach (var itemRequest in request.Items)
            {
                var menuItem = await _repository.GetMenuItemByIdAsync(itemRequest.MenuItemId)
                    ?? throw new NotFoundException($"Menu item '{itemRequest.MenuItemId}' not found");

                if (!menuItem.IsActive)
                {
                    throw new BusinessRuleException($"Menu item '{menuItem.Name}' is not active");
                }

                if (!menuItem.IsAvailable)
                {
                    throw new BusinessRuleException($"Menu item '{menuItem.Name}' is currently unavailable");
                }

                if (menuItem.BranchId != branchId)
                {
                    throw new BusinessRuleException($"Menu item '{menuItem.Name}' does not belong to this branch");
                }

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
                    SpecialRequest = itemRequest.SpecialRequest?.Trim(),
                    KitchenStatus = KitchenStatus.PENDING,
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
                SpecialRequest = item.SpecialRequest,
                KitchenStatus = item.KitchenStatus
            };
        }
    }
}
