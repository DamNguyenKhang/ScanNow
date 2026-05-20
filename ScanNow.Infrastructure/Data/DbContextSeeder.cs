using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Data
{
    public static class DbContextSeeder
    {
        private const string LocalProvider = "Local";
        private const string DefaultAdminEmail = "admin@scannow.local";
        private const string DefaultAdminUsername = "admin";
        private const string DefaultAdminPassword = "Admin@123";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();

            await SeedRolesAsync(roleManager);
            await SeedAdminAsync(userManager, configuration);
        }

        private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
        {
            var roles = new Dictionary<UserRole, string>
            {
                [UserRole.ADMIN] = "Admin",
                [UserRole.OWNER] = "Owner",
                [UserRole.BRANCH_MANAGER] = "Branch manager",
                [UserRole.STAFF] = "Staff",
                [UserRole.KITCHEN] = "Kitchen"
            };

            foreach (var role in roles)
            {
                var roleName = role.Key.ToString();
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                EnsureSucceeded(await roleManager.CreateAsync(new ApplicationRole(roleName)
                {
                    Description = role.Value
                }));
            }
        }

        private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            var adminEmail = configuration["SeedAdmin:Email"] ?? DefaultAdminEmail;
            var adminUsername = configuration["SeedAdmin:Username"] ?? DefaultAdminUsername;
            var adminPassword = configuration["SeedAdmin:Password"] ?? DefaultAdminPassword;

            var admin = await userManager.FindByEmailAsync(adminEmail)
                ?? await userManager.FindByNameAsync(adminUsername);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    Email = adminEmail,
                    UserName = adminUsername,
                    FullName = configuration["SeedAdmin:FullName"] ?? "ScanNow Admin",
                    AuthProvider = LocalProvider,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                EnsureSucceeded(await userManager.CreateAsync(admin, adminPassword));
            }

            var adminRole = UserRole.ADMIN.ToString();
            if (!await userManager.IsInRoleAsync(admin, adminRole))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(admin, adminRole));
            }
        }

        private static void EnsureSucceeded(IdentityResult result)
        {
            if (result.Succeeded)
            {
                return;
            }

            var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
            throw new InvalidOperationException(errors);
        }
    }
}
