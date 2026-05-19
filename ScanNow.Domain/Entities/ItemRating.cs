namespace ScanNow.Domain.Entities
{
    public class ItemRating : Entity<Guid>
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public Guid MenuItemId { get; set; }
        public Guid BranchId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Order Order { get; set; } = null!;
        public OrderItem OrderItem { get; set; } = null!;
        public MenuItem MenuItem { get; set; } = null!;
        public Branch Branch { get; set; } = null!;
    }
}
