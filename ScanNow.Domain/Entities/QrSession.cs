namespace ScanNow.Domain.Entities
{
    public class QrSession : Entity<Guid>
    {
        public Guid TableId { get; set; }
        public Guid BranchId { get; set; }
        public string SessionToken { get; set; } = null!;
        public string? CustomerIdentifier { get; set; }
        public Guid? ActiveOrderId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public RestaurantTable Table { get; set; } = null!;
        public Branch Branch { get; set; } = null!;
        public Order? ActiveOrder { get; set; }
    }
}
