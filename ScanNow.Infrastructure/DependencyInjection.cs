using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScanNow.Domain.Abstractions;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Infrastructure.External;
using ScanNow.Infrastructure.Repositories;
using ScanNow.Infrastructure.Settings;
using ScanNow.Infrastructure.Tenancy;

namespace ScanNow.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // Tenant context: scoped per request so the middleware can populate it
            // and the DbContext can read it for global query filters.
            services.AddScoped<TenantContext>();
            services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            services.AddScoped<IEmailService, SmtpEmailService>();
            services.AddScoped<IPaymentService, PayOSPaymentService>();
            services.AddScoped<IFileStorageService, CloudinaryStorageService>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IUserManagementRepository, UserManagementRepository>();
            services.AddScoped<IRestaurantManagementRepository, RestaurantManagementRepository>();
            services.AddScoped<IMenuManagementRepository, MenuManagementRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IWaiterRepository, WaiterRepository>();
            services.AddScoped<IKitchenRepository, KitchenRepository>();
            services.AddScoped<ITableQrRepository, TableQrRepository>();
            services.AddScoped<IBranchSettingsRepository, BranchSettingsRepository>();
            services.AddScoped<IReportRepository, ReportRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            return services;
        }

        public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            return services;
        }

        public static IServiceCollection AddPayOS(this IServiceCollection services, IConfiguration configuration)
        {
            static string ReadFirstNonEmpty(IConfiguration config, params string[] keys)
            {
                foreach (var key in keys)
                {
                    var value = config[key];
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }

                return string.Empty;
            }

            var clientId = ReadFirstNonEmpty(configuration, "PAYOS_CLIENT_ID", "PayOS:ClientId");
            var apiKey = ReadFirstNonEmpty(configuration, "PAYOS_API_KEY", "PayOS:ApiKey");
            var checksumKey = ReadFirstNonEmpty(configuration, "PAYOS_CHECKSUM_KEY", "PayOS:ChecksumKey");
            var returnUrl = ReadFirstNonEmpty(configuration, "PAYOS_RETURN_URL", "PayOS:ReturnUrl");
            var cancelUrl = ReadFirstNonEmpty(configuration, "PAYOS_CANCEL_URL", "PayOS:CancelUrl");

            services.Configure<PayOSSettings>(options =>
            {
                options.ClientId = clientId;
                options.ApiKey = apiKey;
                options.ChecksumKey = checksumKey;
                options.ReturnUrl = returnUrl;
                options.CancelUrl = cancelUrl;
            });

            return services;
        }
    }
}
