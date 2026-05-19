namespace ScanNow.Domain.Entities
{
    public class Restaurant : Entity<Guid>
    {
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ApplicationUser Owner { get; set; } = null!;
        public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    }
}
