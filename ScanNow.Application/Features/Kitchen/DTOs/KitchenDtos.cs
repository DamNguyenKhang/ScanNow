using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Kitchen.DTOs
{
    // ─── Grouped Kitchen Items ───────────────────────────

    public class GroupedKitchenItemDto
    {
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public int TotalQuantity { get; set; }
        public int AverageCookingMinutes { get; set; }
        public double PriorityScore { get; set; }
        public string SuggestedPriorityLevel { get; set; } = string.Empty;
        public DateTime? OldestConfirmedAt { get; set; }
        public double WaitingMinutes { get; set; }
        public List<GroupedKitchenOrderItemDto> Items { get; set; } = new();
    }

    public class GroupedKitchenOrderItemDto
    {
        public Guid OrderItemId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid? TableId { get; set; }
        public string? TableName { get; set; }
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CookingStartedAt { get; set; }
        public int EstimatedCookingMinutes { get; set; }
    }

    // ─── Start Cooking ───────────────────────────────────

    // ─── Mark Ready ──────────────────────────────────────

    public class MarkReadyRequest
    {
        public List<Guid> OrderItemIds { get; set; } = new();
    }

    public class MarkReadyResponse
    {
        public int ItemsUpdated { get; set; }
        public List<Guid> AffectedOrderIds { get; set; } = new();
    }
}
