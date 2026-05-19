namespace ScanNow.Domain.Entities
{
    public class Branch : Entity<Guid>
    {
        public Guid RestaurantId { get; set; }
        public Guid? ManagerId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
        public bool IsActive { get; set; } = true;
        public decimal VatPercent { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeFixed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Restaurant Restaurant { get; set; } = null!;
        public ApplicationUser? Manager { get; set; }
        public ICollection<BranchStaff> Staffs { get; set; } = new List<BranchStaff>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<QrSession> QrSessions { get; set; } = new List<QrSession>();
        public ICollection<ItemRating> ItemRatings { get; set; } = new List<ItemRating>();
        public ICollection<DiscountCode> DiscountCodes { get; set; } = new List<DiscountCode>();
    }
}
