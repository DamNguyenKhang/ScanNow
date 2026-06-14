using ScanNow.Domain.Abstractions;

namespace ScanNow.Tests.Infrastructure;

/// <summary>
/// Lightweight stub that mimics what TenantResolutionMiddleware sets on a real request.
/// Used to control whether global query filters are active in each test.
/// </summary>
public sealed class TestTenantContext : ITenantContext
{
    public Guid? RestaurantId { get; init; }
    public string? Slug { get; init; }
    public bool IsResolved { get; init; }

    /// <summary>Returns a context where the tenant IS resolved (filters are active).</summary>
    public static TestTenantContext ForRestaurant(Guid restaurantId, string slug = "test") =>
        new() { RestaurantId = restaurantId, Slug = slug, IsResolved = true };

    /// <summary>Returns a context where no tenant is resolved (filters are inactive).</summary>
    public static TestTenantContext Unresolved() =>
        new() { RestaurantId = null, Slug = null, IsResolved = false };
}
