using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Kitchen.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Application.Features.Waiter;
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
                .GroupBy(x => new { x.MenuItemId, x.MenuItemName, x.Status, Note = x.Note ?? string.Empty })
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
                            Status = i.Status.ToString(),
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

        public async Task<StartCookingResponse> StartCookingItemsAsync(StartCookingRequest request, Guid branchId)
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

            // Status validation
            var nonConfirmedItems = items.Where(x => x.Status != OrderItemStatus.Confirmed).ToList();
            if (nonConfirmedItems.Any())
                throw new BusinessRuleException("Only Confirmed items can start cooking. Some items have already been updated.");

            var now = DateTime.UtcNow;
            var affectedOrderIds = new HashSet<Guid>();

            foreach (var item in items)
            {
                item.Status = OrderItemStatus.Cooking;
                item.CookingStartedAt = now;
                item.UpdatedAt = now;
                affectedOrderIds.Add(item.OrderId);
            }

            // Recalculate affected orders
            var orderIds = affectedOrderIds.ToList();
            var orders = await _repository.GetOrdersByIdsAsync(orderIds);
            foreach (var order in orders)
            {
                order.Status = WaiterService.CalculateOrderStatus(order.Items.ToList());
                if (order.Status == OrderStatus.Preparing && !order.PreparingAt.HasValue)
                    order.PreparingAt = now;
                order.UpdatedAt = now;
            }

            await _unitOfWork.SaveChangesAsync();
            foreach (var order in orders)
            {
                await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));
            }

            return new StartCookingResponse
            {
                ItemsUpdated = items.Count,
                AffectedOrderIds = orderIds
            };
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

            // Status validation
            var nonCookingItems = items.Where(x => x.Status != OrderItemStatus.Cooking).ToList();
            if (nonCookingItems.Any())
                throw new BusinessRuleException("Only Cooking items can be marked as Ready. Some items have already been updated.");

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
    }
}
