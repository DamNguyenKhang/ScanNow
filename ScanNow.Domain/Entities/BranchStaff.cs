namespace ScanNow.Domain.Entities
{
    public class BranchStaff : Entity<Guid>
    {
        public Guid BranchId { get; set; }
        public Guid UserId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public Guid? AssignedById { get; set; }

        public Branch Branch { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public ApplicationUser? AssignedBy { get; set; }
    }
}
