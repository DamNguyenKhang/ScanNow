using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.BranchSettings.DTOs
{
    public class BranchPaymentConfigResponse
    {
        public Guid? PaymentConfigId { get; set; }
        public Guid BranchId { get; set; }
        public bool CashEnabled { get; set; } = true;
        public bool PayOsEnabled { get; set; }
        public bool HasPayOsClientId { get; set; }
        public bool HasPayOsApiKey { get; set; }
        public bool HasPayOsChecksumKey { get; set; }
        public string? PayOsClientIdPreview { get; set; }
        public PaymentMethod DefaultMethod { get; set; } = PaymentMethod.CASH;
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpsertBranchPaymentConfigRequest
    {
        public bool CashEnabled { get; set; } = true;
        public bool PayOsEnabled { get; set; }
        public string? PayOsClientId { get; set; }
        public string? PayOsApiKey { get; set; }
        public string? PayOsChecksumKey { get; set; }
        public PaymentMethod DefaultMethod { get; set; } = PaymentMethod.CASH;
    }

    public class PaperVoucherResponse
    {
        public Guid VoucherId { get; set; }
        public Guid BranchId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int Quantity { get; set; }
        public int UsedCount { get; set; }
        public int RemainingCount { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; }
        public string QrPayload { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePaperVoucherRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; } = DiscountType.PERCENT;
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int Quantity { get; set; } = 1;
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdatePaperVoucherRequest : CreatePaperVoucherRequest
    {
    }
}
