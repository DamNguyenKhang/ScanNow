using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        private readonly ITenantContext? _tenantContext;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext? tenantContext = null)
            : base(options)
        {
            _tenantContext = tenantContext;
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
        public DbSet<BranchPaymentConfig> BranchPaymentConfigs => Set<BranchPaymentConfig>();
        public DbSet<PaperVoucher> PaperVouchers => Set<PaperVoucher>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Tenant isolation at the Restaurant level.
            var tenantId = _tenantContext?.RestaurantId;
            var isResolved = _tenantContext?.IsResolved == true;

            // 1. Filter Restaurant
            builder.Entity<Restaurant>().HasQueryFilter(e => !isResolved || e.Id == tenantId);

            // 2. Filter Level 1 (Entities with Branch)
            builder.Entity<Branch>().HasQueryFilter(e => !isResolved || e.RestaurantId == tenantId);
            builder.Entity<BranchStaff>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<Category>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<MenuItem>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<RestaurantTable>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<Order>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<QrSession>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<ItemRating>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<DiscountCode>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<BranchPaymentConfig>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);
            builder.Entity<PaperVoucher>().HasQueryFilter(e => !isResolved || e.Branch.RestaurantId == tenantId);

            // Level 1 Nullable
            builder.Entity<Notification>().HasQueryFilter(e => !isResolved || (e.Branch != null && e.Branch.RestaurantId == tenantId));
            builder.Entity<AuditLog>().HasQueryFilter(e => !isResolved || (e.Branch != null && e.Branch.RestaurantId == tenantId));

            // 3. Filter Level 2 (Entities with Navigation)
            builder.Entity<OrderItem>().HasQueryFilter(e => !isResolved || e.Order.Branch.RestaurantId == tenantId);
            builder.Entity<Payment>().HasQueryFilter(e => !isResolved || e.Order.Branch.RestaurantId == tenantId);
            builder.Entity<MenuItemPriceHistory>().HasQueryFilter(e => !isResolved || e.MenuItem.Branch.RestaurantId == tenantId);
        }
    }
}
