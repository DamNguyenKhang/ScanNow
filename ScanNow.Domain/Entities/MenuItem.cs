namespace ScanNow.Domain.Entities
{
    public class MenuItem : Entity<Guid>
    {
        public Guid BranchId { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int PreparationTime { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Branch Branch { get; set; } = null!;
        public Category Category { get; set; } = null!;
        public ICollection<MenuItemPriceHistory> PriceHistories { get; set; } = new List<MenuItemPriceHistory>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<ItemRating> Ratings { get; set; } = new List<ItemRating>();
    }
}
