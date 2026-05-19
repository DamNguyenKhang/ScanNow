using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<Restaurant> Restaurants => Set<Restaurant>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<BranchStaff> BranchStaff => Set<BranchStaff>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<MenuItem> MenuItems => Set<MenuItem>();
        public DbSet<MenuItemPriceHistory> MenuItemPriceHistory => Set<MenuItemPriceHistory>();
        public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<QrSession> QrSessions => Set<QrSession>();
        public DbSet<ItemRating> ItemRatings => Set<ItemRating>();
        public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
