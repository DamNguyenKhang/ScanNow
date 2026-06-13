using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanNow.Application.DTOs;
using ScanNow.Domain.Tenancy;
using ScanNow.Infrastructure;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/public/tenants")]
    public class PublicTenantsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PublicTenantsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("{slug}")]
        public async Task<ActionResult<ApiResponse<PublicTenantResponse>>> GetTenant(string slug)
        {
            var normalizedSlug = TenantSlugRules.NormalizeTenantSlug(slug);

            if (normalizedSlug is null)
            {
                return NotFound(ApiResponse.Failure("Tenant not found", StatusCodes.Status404NotFound));
            }

            var tenant = await _db.Restaurants
                .AsNoTracking()
                .Where(restaurant => restaurant.Slug == normalizedSlug && restaurant.IsActive)
                .Select(restaurant => new PublicTenantResponse
                {
                    RestaurantId = restaurant.Id,
                    Name = restaurant.Name,
                    Slug = restaurant.Slug,
                    LogoUrl = restaurant.LogoUrl
                })
                .FirstOrDefaultAsync();

            if (tenant is null)
            {
                return NotFound(ApiResponse.Failure("Tenant not found", StatusCodes.Status404NotFound));
            }

            return new ApiResponse<PublicTenantResponse>
            {
                Result = tenant,
                Message = "Get tenant successfully"
            };
        }
    }

    public class PublicTenantResponse
    {
        public Guid RestaurantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }
}
