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
            //
            // Filtering Branch by RestaurantId is the single enforcement point:
            // all sub-entities (orders, menu items, tables …) are reached through a
            // branchId that comes from the URL route parameters already used by every
            // controller. Because Branch is filtered, a branchId belonging to a
            // different restaurant can never be resolved — it simply returns no rows.
            //
            // When _tenantContext is null (migrations / design-time) or IsResolved is
            // false (requests without a subdomain, e.g. admin tools), no filter runs.
            builder.Entity<Branch>()
                .HasQueryFilter(b =>
                    _tenantContext == null ||
                    !_tenantContext.IsResolved ||
                    b.RestaurantId == _tenantContext.RestaurantId!.Value);
        }
    }
}
