using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Kitchen.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Application.Features.Waiter;
using ScanNow.Application.Features.Waiter.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.Kitchen
{
    public class KitchenService : IKitchenService
    {
        private readonly IKitchenRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderUpdatePublisher _publisher;

        public KitchenService(
            IKitchenRepository repository,
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

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
                throw new BusinessRuleException($"Order cannot be confirmed. Current status: {order.Status}");

            var pendingItems = order.Items
                .Where(x => x.Status == OrderItemStatus.Pending)
                .ToList();

            if (!pendingItems.Any())
                throw new BusinessRuleException("This order has no pending items to confirm");

            var now = DateTime.UtcNow;

            foreach (var item in pendingItems)
            {
                item.Status = OrderItemStatus.Confirmed;
                item.ConfirmedAt = now;
                item.UpdatedAt = now;
            }

            order.Status = WaiterService.CalculateOrderStatus(order.Items.ToList());
            order.ConfirmedAt ??= now;
            order.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync();
            await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));

            return new ConfirmOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                ConfirmedAt = order.ConfirmedAt,
                ItemsConfirmed = pendingItems.Count
            };
        }

        public async Task<ConfirmKitchenItemsResponse> ConfirmItemsAsync(ConfirmKitchenItemsRequest request, Guid branchId)
        {
            if (!request.OrderItemIds.Any())
                throw new Domain.Exceptions.ValidationException("OrderItemIds cannot be empty");

            var items = await _repository.GetOrderItemsByIdsAsync(request.OrderItemIds);

            if (items.Count != request.OrderItemIds.Count)
                throw new NotFoundException("One or more order items not found");

            var invalidItems = items.Where(x => x.Order.BranchId != branchId).ToList();
            if (invalidItems.Any())
                throw new ForbiddenException("You do not have permission to confirm these order items");

            var nonPendingItems = items.Where(x => x.Status != OrderItemStatus.Pending).ToList();
            if (nonPendingItems.Any())
                throw new BusinessRuleException("Only Pending items can be confirmed");

            var now = DateTime.UtcNow;
            var affectedOrderIds = new HashSet<Guid>();

            foreach (var item in items)
            {
                item.Status = OrderItemStatus.Confirmed;
                item.ConfirmedAt = now;
                item.UpdatedAt = now;
                affectedOrderIds.Add(item.OrderId);
            }

            var affectedOrders = await _repository.GetOrdersByIdsAsync(affectedOrderIds.ToList());

            foreach (var order in affectedOrders)
            {
                order.Status = WaiterService.CalculateOrderStatus(order.Items.ToList());
                order.ConfirmedAt ??= now;
                order.UpdatedAt = now;
            }

            await _unitOfWork.SaveChangesAsync();

            foreach (var order in affectedOrders)
            {
                await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));
            }

            return new ConfirmKitchenItemsResponse
            {
                ItemsConfirmed = items.Count,
                AffectedOrderIds = affectedOrderIds.ToList()
            };
        }

        public async Task<List<GroupedKitchenItemDto>> GetGroupedKitchenItemsAsync(Guid branchId, string? status = null)
        {
            var items = await _repository.GetActiveKitchenItemsAsync(branchId);

            // Filter by status if provided
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<OrderItemStatus>(status, true, out var parsedStatus))
            {
                items = items.Where(x => x.Status == parsedStatus).ToList();
            }

            var now = DateTime.UtcNow;

            var groups = items
                .GroupBy(x => new
                {
                    x.MenuItemId,
                    x.MenuItemName,
                    Status = x.Status == OrderItemStatus.Cooking ? OrderItemStatus.Confirmed : x.Status,
                    Note = x.Note ?? string.Empty
                })
                .Select(g =>
                {
                    var oldestConfirmedAt = g.Min(x => x.ConfirmedAt);
                    var waitingMinutes = oldestConfirmedAt.HasValue
                        ? (now - oldestConfirmedAt.Value).TotalMinutes
                        : 0;
                    var avgCookingMinutes = g.Average(x => x.EstimatedCookingMinutes);
                    var totalQuantity = g.Sum(x => x.Quantity);

                    var priorityScore = (waitingMinutes * 1.5) + (avgCookingMinutes * 1.0) + (totalQuantity * 0.5);

                    return new GroupedKitchenItemDto
                    {
                        MenuItemId = g.Key.MenuItemId,
                        MenuItemName = g.Key.MenuItemName,
                        Status = g.Key.Status.ToString(),
                        Note = string.IsNullOrEmpty(g.Key.Note) ? null : g.Key.Note,
                        TotalQuantity = totalQuantity,
                        AverageCookingMinutes = (int)Math.Round(avgCookingMinutes),
                        PriorityScore = Math.Round(priorityScore, 2),
                        SuggestedPriorityLevel = priorityScore >= 40 ? "High" : priorityScore >= 20 ? "Medium" : "Low",
                        OldestConfirmedAt = oldestConfirmedAt,
                        WaitingMinutes = Math.Round(waitingMinutes, 1),
                        Items = g.Select(i => new GroupedKitchenOrderItemDto
                        {
                            OrderItemId = i.Id,
                            OrderId = i.OrderId,
                            OrderCode = i.Order.OrderNumber,
                            TableId = i.Order.TableId,
                            TableName = i.Order.Table?.TableNumber,
                            Quantity = i.Quantity,
                            Note = i.Note,
                            Status = i.Status == OrderItemStatus.Cooking
                                ? OrderItemStatus.Confirmed.ToString()
                                : i.Status.ToString(),
                            ConfirmedAt = i.ConfirmedAt,
                            CookingStartedAt = i.CookingStartedAt,
                            EstimatedCookingMinutes = i.EstimatedCookingMinutes
                        }).ToList()
                    };
                })
                .OrderByDescending(x => x.PriorityScore)
                .ThenBy(x => x.OldestConfirmedAt)
                .ThenByDescending(x => x.AverageCookingMinutes)
                .ToList();

            return groups;
        }

        public async Task<MarkReadyResponse> MarkItemsReadyAsync(MarkReadyRequest request, Guid branchId)
        {
            if (!request.OrderItemIds.Any())
                throw new Domain.Exceptions.ValidationException("OrderItemIds cannot be empty");

            var items = await _repository.GetOrderItemsByIdsAsync(request.OrderItemIds);

            if (items.Count != request.OrderItemIds.Count)
                throw new NotFoundException("One or more order items not found");

            // Branch isolation
            var invalidItems = items.Where(x => x.Order.BranchId != branchId).ToList();
            if (invalidItems.Any())
                throw new ForbiddenException("You do not have permission to update these order items");

            var invalidStatusItems = items
                .Where(x => x.Status != OrderItemStatus.Confirmed && x.Status != OrderItemStatus.Cooking)
                .ToList();
            if (invalidStatusItems.Any())
                throw new BusinessRuleException("Only confirmed items can be marked as Ready. Some items have already been updated.");

            var now = DateTime.UtcNow;
            var affectedOrderIds = new HashSet<Guid>();

            foreach (var item in items)
            {
                item.Status = OrderItemStatus.Ready;
                item.ReadyAt = now;
                item.UpdatedAt = now;
                affectedOrderIds.Add(item.OrderId);
            }

            // Recalculate affected orders
            var orderIds = affectedOrderIds.ToList();
            var orders = await _repository.GetOrdersByIdsAsync(orderIds);
            foreach (var order in orders)
            {
                var newStatus = WaiterService.CalculateOrderStatus(order.Items.ToList());
                order.Status = newStatus;
                if ((newStatus == OrderStatus.ReadyToServe || newStatus == OrderStatus.PartiallyReady)
                    && !order.ReadyAt.HasValue)
                    order.ReadyAt = now;
                order.UpdatedAt = now;
            }

            await _unitOfWork.SaveChangesAsync();
            foreach (var order in orders)
            {
                await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));
            }

            return new MarkReadyResponse
            {
                ItemsUpdated = items.Count,
                AffectedOrderIds = orderIds
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
                Items = order.Items
                    .Where(i => i.Status == OrderItemStatus.Pending)
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new PendingOrderItemResponse
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
                    })
                    .ToList()
            };
        }
    }
}
