using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class AuditLog : Entity<Guid>
    {
        public Guid? UserId { get; set; }
        public Guid? BranchId { get; set; }
        public AuditAction Action { get; set; }
        public string EntityType { get; set; } = null!;
        public Guid? EntityId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }
        public Branch? Branch { get; set; }
    }
}
