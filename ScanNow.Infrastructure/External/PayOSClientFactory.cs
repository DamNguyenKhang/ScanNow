using Microsoft.Extensions.Logging;
using PayOS;

namespace ScanNow.Infrastructure.External
{
    internal static class PayOSClientFactory
    {
        public static PayOSClient Create(string clientId, string apiKey, string checksumKey)
        {
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                Environment.SetEnvironmentVariable("PAYOS_CLIENT_ID", clientId.Trim());
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Environment.SetEnvironmentVariable("PAYOS_API_KEY", apiKey.Trim());
            }

            if (!string.IsNullOrWhiteSpace(checksumKey))
            {
                Environment.SetEnvironmentVariable("PAYOS_CHECKSUM_KEY", checksumKey.Trim());
            }

            return new PayOSClient(new PayOSOptions
            {
                ClientId = clientId,
                ApiKey = apiKey,
                ChecksumKey = checksumKey,
                LogLevel = LogLevel.Debug,
            });
        }
    }
}
