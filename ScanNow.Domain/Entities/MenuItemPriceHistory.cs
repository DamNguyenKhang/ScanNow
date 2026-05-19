namespace ScanNow.Domain.Entities
{
    public class MenuItemPriceHistory : Entity<Guid>
    {
        public Guid MenuItemId { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public Guid ChangedById { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; }

        public MenuItem MenuItem { get; set; } = null!;
        public ApplicationUser ChangedBy { get; set; } = null!;
    }
}
