using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class BranchPaymentConfig : Entity<Guid>
    {
        public Guid BranchId { get; set; }
        public bool CashEnabled { get; set; } = true;
        public bool PayOsEnabled { get; set; }
        public string? PayOsClientId { get; set; }
        public string? PayOsApiKey { get; set; }
        public string? PayOsChecksumKey { get; set; }
        public PaymentMethod DefaultMethod { get; set; } = PaymentMethod.CASH;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Branch Branch { get; set; } = null!;
    }
}
