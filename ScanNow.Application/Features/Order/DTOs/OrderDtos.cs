using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Order.DTOs
{
    // ─── Request ────────────────────────────────────────

    public class PlaceOrderRequest
    {
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerNote { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public Guid MenuItemId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Note { get; set; }
    }

    // ─── Response ───────────────────────────────────────

    public class OrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public Guid? TableId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerNote { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatPercent { get; set; }
        public decimal VatAmount { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public OrderSource OrderSource { get; set; }
        public List<OrderItemResponse> Items { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class OrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
        public OrderItemStatus Status { get; set; }
        public int EstimatedCookingMinutes { get; set; }
    }

    public class CustomerOrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public Guid? TableId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerNote { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatPercent { get; set; }
        public decimal VatAmount { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public OrderSource OrderSource { get; set; }
        public List<CustomerOrderItemResponse> Items { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CustomerOrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
        public int EstimatedCookingMinutes { get; set; }
    }
}
