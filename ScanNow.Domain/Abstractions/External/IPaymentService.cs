namespace ScanNow.Domain.Abstractions.External
{
    public interface IPaymentService
    {
        Task<PaymentLinkResult> CreatePaymentLinkAsync(CreatePaymentLinkInput input);
        Task<PaymentStatusResult> GetPaymentStatusAsync(long orderCode);
    }

    public class CreatePaymentLinkInput
    {
        public long OrderCode { get; set; }
        public long Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? BuyerName { get; set; }
        public string? BuyerPhone { get; set; }
        public int ExpiredAtUnixSeconds { get; set; }
    }

    public class PaymentLinkResult
    {
        public bool Success { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? QrCode { get; set; }
        public string? Bin { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public long? Amount { get; set; }
        public string? Description { get; set; }
        public string? ErrorMessage { get; set; }

        public static PaymentLinkResult Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }

    public class PaymentStatusResult
    {
        public bool IsPaid { get; set; }
        public string? Status { get; set; }
        public string? TransactionId { get; set; }
        public string? ErrorMessage { get; set; }

        public static PaymentStatusResult Paid(string? transactionId = null) =>
            new() { IsPaid = true, Status = "PAID", TransactionId = transactionId };

        public static PaymentStatusResult NotPaid(string status) =>
            new() { IsPaid = false, Status = status };

        public static PaymentStatusResult Error(string error) =>
            new() { IsPaid = false, ErrorMessage = error };
    }
}
