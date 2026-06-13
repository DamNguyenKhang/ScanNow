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

        // Properties for EF Core query filter parameterization
        public Guid? CurrentTenantId => _tenantContext?.RestaurantId;
        public bool IsTenantResolved => _tenantContext?.IsResolved == true;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // 1. Filter Restaurant
            builder.Entity<Restaurant>().HasQueryFilter(e => !IsTenantResolved || e.Id == CurrentTenantId);

            // 2. Filter Level 1 (Entities with Branch)
            builder.Entity<Branch>().HasQueryFilter(e => !IsTenantResolved || e.RestaurantId == CurrentTenantId);
            builder.Entity<BranchStaff>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<Category>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<MenuItem>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<RestaurantTable>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<Order>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<QrSession>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<ItemRating>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<DiscountCode>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<BranchPaymentConfig>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<PaperVoucher>().HasQueryFilter(e => !IsTenantResolved || e.Branch.RestaurantId == CurrentTenantId);

            // Level 1 Nullable
            builder.Entity<Notification>().HasQueryFilter(e => !IsTenantResolved || (e.Branch != null && e.Branch.RestaurantId == CurrentTenantId));
            builder.Entity<AuditLog>().HasQueryFilter(e => !IsTenantResolved || (e.Branch != null && e.Branch.RestaurantId == CurrentTenantId));

            // 3. Filter Level 2 (Entities with Navigation)
            builder.Entity<OrderItem>().HasQueryFilter(e => !IsTenantResolved || e.Order.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<Payment>().HasQueryFilter(e => !IsTenantResolved || e.Order.Branch.RestaurantId == CurrentTenantId);
            builder.Entity<MenuItemPriceHistory>().HasQueryFilter(e => !IsTenantResolved || e.MenuItem.Branch.RestaurantId == CurrentTenantId);
        }
    }
}
