using Microsoft.AspNetCore.Identity;

namespace ScanNow.Domain.Entities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string AuthProvider { get; set; } = "Local";

        public bool IsActive { get; set; } = true;

        public DateTime? LastLoginAt { get; set; }

        public bool ForcePasswordChange { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
        public virtual ICollection<Restaurant> OwnedRestaurants { get; set; } = new List<Restaurant>();
        public virtual ICollection<Branch> ManagedBranches { get; set; } = new List<Branch>();
        public virtual ICollection<BranchStaff> BranchStaffAssignments { get; set; } = new List<BranchStaff>();
        public virtual ICollection<BranchStaff> AssignedBranchStaffs { get; set; } = new List<BranchStaff>();
        public virtual ICollection<MenuItemPriceHistory> PriceChanges { get; set; } = new List<MenuItemPriceHistory>();
        public virtual ICollection<Order> ConfirmedOrders { get; set; } = new List<Order>();
        public virtual ICollection<Order> CancelledOrders { get; set; } = new List<Order>();
        public virtual ICollection<Payment> RefundedPayments { get; set; } = new List<Payment>();
        public virtual ICollection<Payment> ProcessedPayments { get; set; } = new List<Payment>();
        public virtual ICollection<Notification> TargetNotifications { get; set; } = new List<Notification>();
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
