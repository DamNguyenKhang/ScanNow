using ScanNow.Domain.Abstractions;

namespace ScanNow.Infrastructure.Tenancy
{
    public class TenantContext : ITenantContext
    {
        public Guid? RestaurantId { get; set; }
        public string? Slug { get; set; }
        public bool IsResolved => RestaurantId.HasValue;
    }
}
