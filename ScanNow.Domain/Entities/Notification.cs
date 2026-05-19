using ScanNow.Domain.Enums;

namespace ScanNow.Domain.Entities
{
    public class Notification : Entity<Guid>
    {
        public Guid? BranchId { get; set; }
        public Guid? OrderId { get; set; }
        public NotificationTargetType TargetType { get; set; }
        public Guid? TargetUserId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public string? Data { get; set; }
        public NotificationChannel Channel { get; set; } = NotificationChannel.SIGNALR;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Branch? Branch { get; set; }
        public Order? Order { get; set; }
        public ApplicationUser? TargetUser { get; set; }
    }
}
