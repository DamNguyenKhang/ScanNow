using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Checkout.DTOs
{
    // ─── Request ────────────────────────────────────────

    public class CreateCheckoutRequest
    {
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PAYOS;
    }

    // ─── Response ───────────────────────────────────────

    public class CheckoutResponse
    {
        public Guid OrderId { get; set; }
        public Guid PaymentId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public string? Bin { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public long? Amount { get; set; }
        public string? Description { get; set; }
    }

    public class PaymentStatusResponse
    {
        public Guid OrderId { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
    }
}
