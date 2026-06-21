using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ScanNow.Application.Abstractions;
using ScanNow.Domain.Tenancy;

namespace ScanNow.Application.Features.Common
{
    public class TenantUrlBuilder : ITenantUrlBuilder
    {
        private readonly IConfiguration _configuration;

        public TenantUrlBuilder(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string GetFallbackBaseUrl()
        {
            var url = _configuration["App:ClientUrl"] 
                      ?? _configuration["App:FrontendBaseUrl"] 
                      ?? "https://scannow.site";
            return url.TrimEnd('/');
        }

        private string GetTenantBaseDomain()
        {
            return _configuration["App:TenantBaseDomain"]?.Trim().ToLowerInvariant() ?? "";
        }

        public string BuildTenantBaseUrl(string? slug)
        {
            var normalizedSlug = TenantSlugRules.NormalizeTenantSlug(slug);
            var tenantBaseDomain = GetTenantBaseDomain();

            if (string.IsNullOrEmpty(normalizedSlug) || string.IsNullOrEmpty(tenantBaseDomain))
            {
                return GetFallbackBaseUrl();
            }

            return $"https://{normalizedSlug}.{tenantBaseDomain}";
        }

        public string BuildTenantTableUrl(string? slug, string qrCodeToken)
        {
            var baseTenantUrl = BuildTenantBaseUrl(slug);
            var tablePath = _configuration["App:QrTablePath"]?.Trim('/') ?? "tables";
            return $"{baseTenantUrl}/{tablePath}/{Uri.EscapeDataString(qrCodeToken)}";
        }

        public string BuildTenantPaymentUrl(string? slug, string result, Dictionary<string, string>? queryParameters = null)
        {
            var baseTenantUrl = BuildTenantBaseUrl(slug);
            var url = $"{baseTenantUrl}/payment/{result.Trim('/')}";

            if (queryParameters != null && queryParameters.Count > 0)
            {
                var querySegments = queryParameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
                url += "?" + string.Join("&", querySegments);
            }

            return url;
        }

        public string BuildTenantPaymentUrl(string? slug, string result, string? query)
        {
            var baseTenantUrl = BuildTenantBaseUrl(slug);
            var url = $"{baseTenantUrl}/payment/{result.Trim('/')}";

            if (!string.IsNullOrWhiteSpace(query))
            {
                var trimmedQuery = query.TrimStart('?');
                url += "?" + trimmedQuery;
            }

            return url;
        }

        public string BuildPlatformUrl(string? path)
        {
            var platformBaseUrl = GetFallbackBaseUrl();
            if (string.IsNullOrWhiteSpace(path))
            {
                return platformBaseUrl;
            }
            return $"{platformBaseUrl}/{path.Trim('/')}";
        }
    }
}
