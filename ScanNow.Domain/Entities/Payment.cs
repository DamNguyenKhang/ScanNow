using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class Payment : Entity<Guid>
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
        public decimal? AmountReceived { get; set; }
        public decimal? ChangeAmount { get; set; }
        public string? TransactionId { get; set; }
        public string? GatewayOrderId { get; set; }
        public string? GatewayResponseCode { get; set; }
        public string? GatewayResponseData { get; set; }
        public string? PaymentUrl { get; set; }
        public string? RefundTransactionId { get; set; }
        public string? RefundReason { get; set; }
        public DateTime? RefundedAt { get; set; }
        public Guid? RefundedById { get; set; }
        public Guid? ProcessedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Order Order { get; set; } = null!;
        public ApplicationUser? RefundedBy { get; set; }
        public ApplicationUser? ProcessedBy { get; set; }
    }
}
