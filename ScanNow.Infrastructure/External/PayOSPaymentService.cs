using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Infrastructure.Settings;

namespace ScanNow.Infrastructure.External
{
    public class PayOSPaymentService : IPaymentService
    {
        private readonly PayOSClient? _client;
        private readonly string? _initError;
        private readonly PayOSSettings _settings;
        private readonly ILogger<PayOSPaymentService> _logger;

        public PayOSPaymentService(
            IOptions<PayOSSettings> settings,
            ILogger<PayOSPaymentService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            try
            {
                _client = PayOSClientFactory.Create(
                    _settings.ClientId,
                    _settings.ApiKey,
                    _settings.ChecksumKey);
            }
            catch (Exception ex)
            {
                _initError = ex.Message;
                _logger.LogError(ex, "Could not initialize PayOS payment client.");
            }
        }

        public async Task<PaymentLinkResult> CreatePaymentLinkAsync(CreatePaymentLinkInput input)
        {
            var client = _client;
            var initError = _initError;

            if (!string.IsNullOrWhiteSpace(input.PayOsClientId)
                && !string.IsNullOrWhiteSpace(input.PayOsApiKey)
                && !string.IsNullOrWhiteSpace(input.PayOsChecksumKey))
            {
                try
                {
                    client = PayOSClientFactory.Create(input.PayOsClientId, input.PayOsApiKey, input.PayOsChecksumKey);
                    initError = null;
                }
                catch (Exception ex)
                {
                    initError = ex.Message;
                    _logger.LogError(ex, "Could not initialize branch PayOS payment client.");
                }
            }

            if (client == null)
            {
                return PaymentLinkResult.Error(initError ?? "PayOS client is not initialized.");
            }

            try
            {
                var request = new CreatePaymentLinkRequest
                {
                    OrderCode = input.OrderCode,
                    Amount = input.Amount,
                    Description = input.Description,
                    ReturnUrl = input.ReturnUrl ?? _settings.ReturnUrl,
                    CancelUrl = input.CancelUrl ?? _settings.CancelUrl,
                    ExpiredAt = input.ExpiredAtUnixSeconds,
                    BuyerName = input.BuyerName,
                    BuyerPhone = input.BuyerPhone
                };

                var response = await client.PaymentRequests.CreateAsync(request);

                _logger.LogInformation(
                    "PayOS payment link created for order {OrderCode}: {CheckoutUrl}",
                    input.OrderCode, response.CheckoutUrl);

                return new PaymentLinkResult
                {
                    Success = true,
                    CheckoutUrl = response.CheckoutUrl,
                    PaymentLinkId = response.PaymentLinkId,
                    QrCode = response.QrCode,
                    Bin = response.Bin,
                    AccountNumber = response.AccountNumber,
                    AccountName = response.AccountName,
                    Amount = response.Amount,
                    Description = response.Description
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS CreatePaymentLink failed for order {OrderCode}", input.OrderCode);
                return PaymentLinkResult.Error(ex.Message);
            }
        }

        public async Task<PaymentStatusResult> GetPaymentStatusAsync(long orderCode, PayOSCredentialInput? credentials = null)
        {
            var client = _client;
            var initError = _initError;

            if (!string.IsNullOrWhiteSpace(credentials?.ClientId)
                && !string.IsNullOrWhiteSpace(credentials.ApiKey)
                && !string.IsNullOrWhiteSpace(credentials.ChecksumKey))
            {
                try
                {
                    client = PayOSClientFactory.Create(credentials.ClientId, credentials.ApiKey, credentials.ChecksumKey);
                    initError = null;
                }
                catch (Exception ex)
                {
                    initError = ex.Message;
                    _logger.LogError(ex, "Could not initialize branch PayOS payment client for status check.");
                }
            }

            if (client == null)
            {
                return PaymentStatusResult.Error(initError ?? "PayOS client is not initialized.");
            }

            try
            {
                var paymentInfo = await client.PaymentRequests.GetAsync(orderCode);

                if (paymentInfo == null)
                    return PaymentStatusResult.Error("Could not retrieve payment info from PayOS.");

                var statusStr = paymentInfo.Status.ToString().ToUpperInvariant();

                _logger.LogInformation(
                    "PayOS payment status for order {OrderCode}: {Status}",
                    orderCode, statusStr);

                if (statusStr == "PAID")
                {
                    return PaymentStatusResult.Paid(paymentInfo.Id.ToString());
                }

                return PaymentStatusResult.NotPaid(statusStr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying PayOS for order {OrderCode}", orderCode);
                return PaymentStatusResult.Error(ex.Message);
            }
        }
    }
}
