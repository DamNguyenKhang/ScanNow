using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class DiscountCode : Entity<Guid>
    {
        public Guid BranchId { get; set; }
        public string Code { get; set; } = null!;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Branch Branch { get; set; } = null!;
    }
}
