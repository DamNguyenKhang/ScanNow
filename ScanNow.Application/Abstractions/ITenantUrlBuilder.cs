using System.Collections.Generic;

namespace ScanNow.Application.Abstractions
{
    public interface ITenantUrlBuilder
    {
        string BuildTenantBaseUrl(string? slug);
        string BuildTenantTableUrl(string? slug, string qrCodeToken);
        string BuildTenantPaymentUrl(string? slug, string result, Dictionary<string, string>? queryParameters = null);
        string BuildTenantPaymentUrl(string? slug, string result, string? query);
        string BuildPlatformUrl(string? path);
    }
}
