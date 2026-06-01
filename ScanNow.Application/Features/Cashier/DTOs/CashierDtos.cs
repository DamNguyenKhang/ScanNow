using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.Cashier.DTOs
{
    public class CashierOrderQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Status { get; set; } = "active";
        public string? SortBy { get; set; } = "createdAt";
        public string? SortDirection { get; set; } = "desc";
    }

    public class CashierCheckoutRequest
    {
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CASH;
        public string? VoucherCode { get; set; }
        public decimal? AmountReceived { get; set; }
    }

    public class CashierPaymentResponse
    {
        public Guid OrderId { get; set; }
        public Guid PaymentId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public string? Bin { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public long? Amount { get; set; }
        public decimal? AmountReceived { get; set; }
        public decimal? ChangeAmount { get; set; }
        public string? Description { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }
        public TableOrderHistoryResponse Order { get; set; } = null!;
    }
}
