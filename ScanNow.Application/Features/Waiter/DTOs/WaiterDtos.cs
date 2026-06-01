using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Waiter.DTOs
{
    // ─── Pending Orders ─────────────────────────────────

    public class PendingOrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public Guid? TableId { get; set; }
        public string? TableNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerNote { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PendingOrderItemResponse> Items { get; set; } = new();
    }

    public class PendingOrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
        public OrderItemStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ─── Confirm Order ───────────────────────────────────

    public class ConfirmOrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public int ItemsConfirmed { get; set; }
    }

    // ─── Ready to Serve ──────────────────────────────────

    public class ReadyToServeTableGroup
    {
        public Guid? TableId { get; set; }
        public string? TableNumber { get; set; }
        public List<ReadyToServeOrderGroup> Orders { get; set; } = new();
    }

    public class ReadyToServeOrderGroup
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public List<ReadyToServeItemResponse> Items { get; set; } = new();
    }

    public class ReadyToServeItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public DateTime? ReadyAt { get; set; }
    }

    // ─── Mark Served ─────────────────────────────────────

    public class MarkItemsServedRequest
    {
        public List<Guid> OrderItemIds { get; set; } = new();
    }

    public class MarkItemsServedResponse
    {
        public int ItemsServed { get; set; }
        public List<Guid> AffectedOrderIds { get; set; } = new();
    }
}
