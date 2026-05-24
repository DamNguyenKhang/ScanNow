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
            if (_client == null)
            {
                return PaymentLinkResult.Error(_initError ?? "PayOS client is not initialized.");
            }

            try
            {
                var request = new CreatePaymentLinkRequest
                {
                    OrderCode = input.OrderCode,
                    Amount = input.Amount,
                    Description = input.Description,
                    ReturnUrl = _settings.ReturnUrl,
                    CancelUrl = _settings.CancelUrl,
                    ExpiredAt = input.ExpiredAtUnixSeconds,
                    BuyerName = input.BuyerName,
                    BuyerPhone = input.BuyerPhone
                };

                var response = await _client.PaymentRequests.CreateAsync(request);

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

        public async Task<PaymentStatusResult> GetPaymentStatusAsync(long orderCode)
        {
            if (_client == null)
            {
                return PaymentStatusResult.Error(_initError ?? "PayOS client is not initialized.");
            }

            try
            {
                var paymentInfo = await _client.PaymentRequests.GetAsync(orderCode);

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
