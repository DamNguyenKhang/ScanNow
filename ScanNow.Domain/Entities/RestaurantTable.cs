using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class RestaurantTable : Entity<Guid>
    {
        public Guid BranchId { get; set; }
        public string TableNumber { get; set; } = null!;
        public int Capacity { get; set; } = 4;
        public string QrCodeToken { get; set; } = null!;
        public string? QrCodeUrl { get; set; }
        public string? QrCodeImageUrl { get; set; }
        public TableStatus Status { get; set; } = TableStatus.AVAILABLE;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Branch Branch { get; set; } = null!;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<QrSession> QrSessions { get; set; } = new List<QrSession>();
    }
}
