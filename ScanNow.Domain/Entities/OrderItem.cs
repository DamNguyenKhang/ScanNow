using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class OrderItem : Entity<Guid>
    {
        public Guid OrderId { get; set; }
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = null!;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal SubTotal { get; set; }
        public string? SpecialRequest { get; set; }
        public KitchenStatus KitchenStatus { get; set; } = KitchenStatus.PENDING;
        public string? KitchenNote { get; set; }
        public DateTime? PreparedAt { get; set; }
        public DateTime? ServedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Order Order { get; set; } = null!;
        public MenuItem MenuItem { get; set; } = null!;
        public ICollection<ItemRating> Ratings { get; set; } = new List<ItemRating>();
    }
}
