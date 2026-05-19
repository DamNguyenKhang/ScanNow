using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class Order : Entity<Guid>
    {
        public Guid BranchId { get; set; }
        public Guid? TableId { get; set; }
        public string OrderNumber { get; set; } = null!;
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerNote { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatPercent { get; set; }
        public decimal VatAmount { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.PENDING;
        public OrderSource OrderSource { get; set; } = OrderSource.QR;
        public string? StaffNote { get; set; }
        public Guid? ConfirmedById { get; set; }
        public Guid? CancelledById { get; set; }
        public string? CancelReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? PreparingAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Branch Branch { get; set; } = null!;
        public RestaurantTable? Table { get; set; }
        public ApplicationUser? ConfirmedBy { get; set; }
        public ApplicationUser? CancelledBy { get; set; }
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<QrSession> QrSessions { get; set; } = new List<QrSession>();
        public ICollection<ItemRating> Ratings { get; set; } = new List<ItemRating>();
    }
}
