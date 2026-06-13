namespace ScanNow.Domain.Tenancy
{
    public static class TenantSlugRules
    {
        private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "www", "api", "admin", "app", "business", "localhost", "staging"
        };

        public static string? NormalizeTenantSlug(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var normalized = slug.Trim().ToLowerInvariant();
            return ReservedSlugs.Contains(normalized) ? null : normalized;
        }
    }
}
