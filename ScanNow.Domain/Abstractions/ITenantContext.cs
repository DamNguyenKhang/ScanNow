namespace ScanNow.Domain.Abstractions
{
    /// <summary>
    /// Holds the resolved tenant identity for the current HTTP request.
    /// Populated by <c>TenantResolutionMiddleware</c> from the subdomain or
    /// the <c>X-Tenant-Slug</c> header sent by the frontend.
    ///
    /// Tenant granularity = Restaurant.
    /// Branch identification is handled separately via URL route parameters
    /// (e.g. /api/public/branches/{branchId}) as already used by all controllers.
    /// </summary>
    public interface ITenantContext
    {
        /// <summary>The Restaurant this request belongs to (resolved from subdomain).</summary>
        Guid? RestaurantId { get; }

        /// <summary>The Restaurant's slug (e.g. "pho24").</summary>
        string? Slug { get; }

        /// <summary>True when a Restaurant has been successfully resolved for this request.</summary>
        bool IsResolved { get; }
    }
}
