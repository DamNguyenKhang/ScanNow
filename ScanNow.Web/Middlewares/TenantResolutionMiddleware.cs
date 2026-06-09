using Microsoft.EntityFrameworkCore;
using ScanNow.Infrastructure;
using ScanNow.Infrastructure.Tenancy;

namespace ScanNow.Web.Middlewares
{
    /// <summary>
    /// Resolves the current tenant (Restaurant) from the incoming request and
    /// populates <see cref="TenantContext"/> so that EF Core global query filters
    /// automatically scope Branch queries to that restaurant.
    ///
    /// Tenant granularity = Restaurant (subdomain).
    /// Branch identification is handled by URL route params already used by all controllers
    /// (e.g. /api/public/branches/{branchId}). The EF filter on Branch ensures a branchId
    /// from a different restaurant cannot be used — it will simply return no results.
    ///
    /// Resolution order (first match wins):
    ///   1. X-Tenant-Slug request header  ← set by the frontend when FE and API are on
    ///      different domains (e.g. pho24.scannow.vn → api.scannow.vn).
    ///   2. Request Host subdomain         ← when the API itself is on a tenant subdomain.
    /// </summary>
    public class TenantResolutionMiddleware
    {
        private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "www", "api", "admin", "app", "localhost", "staging"
        };

        private const string TenantSlugHeader = "X-Tenant-Slug";

        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var slug = ResolveSlug(context);

            if (slug is not null)
            {
                var tenantContext = context.RequestServices.GetRequiredService<TenantContext>();
                var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();

                var restaurant = await db.Restaurants
                    .AsNoTracking()
                    .Where(r => r.Slug == slug && r.IsActive)
                    .Select(r => new { r.Id, r.Slug })
                    .FirstOrDefaultAsync();

                if (restaurant is not null)
                {
                    tenantContext.RestaurantId = restaurant.Id;
                    tenantContext.Slug = restaurant.Slug;
                }
            }

            await _next(context);
        }

        private static string? ResolveSlug(HttpContext context)
        {
            // Priority 1: explicit header sent by the frontend.
            var headerSlug = context.Request.Headers[TenantSlugHeader].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerSlug))
                return headerSlug.Trim();

            // Priority 2: subdomain from the Host header.
            return ExtractSubdomain(context.Request.Host.Host);
        }

        private static string? ExtractSubdomain(string host)
        {
            var cleanHost = host.Contains(':') ? host[..host.IndexOf(':')] : host;
            var parts = cleanHost.Split('.');

            if (parts.Length < 3) return null;

            var subdomain = parts[0];
            return ReservedSubdomains.Contains(subdomain) ? null : subdomain;
        }
    }
}
