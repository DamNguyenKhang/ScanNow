using FluentValidation;
using Microsoft.Extensions.Configuration;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Checkout.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;
using System.Text.Json;

namespace ScanNow.Application.Features.Checkout
{
    public class CheckoutService : ICheckoutService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentService _paymentService;
        private readonly IBranchSettingsRepository _branchSettingsRepository;
        private readonly IValidator<CreateCheckoutRequest> _checkoutValidator;
        private readonly IOrderUpdatePublisher _publisher;
        private readonly IConfiguration _configuration;
        private readonly ITenantUrlBuilder _urlBuilder;

        public CheckoutService(
            IOrderRepository orderRepository,
            IUnitOfWork unitOfWork,
            IPaymentService paymentService,
            IBranchSettingsRepository branchSettingsRepository,
            IValidator<CreateCheckoutRequest> checkoutValidator,
            IOrderUpdatePublisher publisher,
            IConfiguration configuration,
            ITenantUrlBuilder urlBuilder)
        {
            _orderRepository = orderRepository;
            _unitOfWork = unitOfWork;
            _paymentService = paymentService;
            _branchSettingsRepository = branchSettingsRepository;
            _checkoutValidator = checkoutValidator;
            _publisher = publisher;
            _configuration = configuration;
            _urlBuilder = urlBuilder;
        }

        public async Task<CheckoutResponse> CreateCheckoutAsync(string sessionCode, CreateCheckoutRequest request)
        {
            await _checkoutValidator.ValidateAndThrowAsync(request);

            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var session = await _orderRepository.GetActiveSessionByCodeAsync(normalizedCode)
                ?? throw new NotFoundException("Session not found or expired");

            if (!session.ActiveOrderId.HasValue)
            {
                throw new BusinessRuleException("No active order found for this session. Please place an order first.");
            }

            var order = await _orderRepository.GetOrderWithDetailsAsync(session.ActiveOrderId.Value)
                ?? throw new NotFoundException("Order not found");

            if (order.Status == OrderStatus.Completed)
            {
                throw new ConflictException($"Order is already in '{order.Status}' status and cannot be checked out.");
            }

            var existingPayment = order.Payments
                .Where(p => p.Status == PaymentStatus.SUCCESS || p.Status == PaymentStatus.PENDING)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .FirstOrDefault();

            if (existingPayment is { Status: PaymentStatus.SUCCESS })
            {
                throw new ConflictException("This order has already been paid.");
            }

            if (existingPayment is { Status: PaymentStatus.PENDING, Method: PaymentMethod.PAYOS })
            {
                return BuildCheckoutResponse(order, existingPayment);
            }

            if (request.PaymentMethod == PaymentMethod.CASH)
            {
                return await HandleCashPaymentAsync(order);
            }

            return await HandlePayOSPaymentAsync(order, session);
        }

        public async Task<PaymentStatusResponse> GetPaymentStatusAsync(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var session = await _orderRepository.GetActiveSessionByCodeAsync(normalizedCode)
                ?? throw new NotFoundException("Session not found or expired");

            if (!session.ActiveOrderId.HasValue)
            {
                throw new BusinessRuleException("No active order found for this session.");
            }

            var order = await _orderRepository.GetOrderWithPaymentsAsync(session.ActiveOrderId.Value)
                ?? throw new NotFoundException("Order not found");

            var payment = order.Payments
                .Where(p => p.Method == PaymentMethod.PAYOS)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (payment == null)
            {
                return new PaymentStatusResponse
                {
                    OrderId = order.Id,
                    PaymentStatus = "NO_PAYMENT",
                    OrderStatus = order.Status.ToString()
                };
            }

            if (payment.Status == PaymentStatus.SUCCESS)
            {
                return new PaymentStatusResponse
                {
                    OrderId = order.Id,
                    PaymentStatus = PaymentStatus.SUCCESS.ToString(),
                    OrderStatus = order.Status.ToString()
                };
            }

            if (payment.Status == PaymentStatus.PENDING && !string.IsNullOrEmpty(payment.GatewayOrderId))
            {
                if (long.TryParse(payment.GatewayOrderId, out var orderCode))
                {
                    var config = await _branchSettingsRepository.GetPaymentConfigAsync(order.BranchId);
                    var gatewayResult = await _paymentService.GetPaymentStatusAsync(
                        orderCode,
                        new PayOSCredentialInput
                        {
                            ClientId = config?.PayOsClientId,
                            ApiKey = config?.PayOsApiKey,
                            ChecksumKey = config?.PayOsChecksumKey
                        });

                    if (gatewayResult.IsPaid)
                    {
                        var now = DateTime.UtcNow;
                        await _orderRepository.MarkPaymentSucceededAsync(payment.Id, gatewayResult.TransactionId, now);
                        await _orderRepository.MarkOrderCompletedAsync(order.Id, now);
                        await _unitOfWork.SaveChangesAsync();

                        payment.Status = PaymentStatus.SUCCESS;
                        payment.TransactionId = gatewayResult.TransactionId;
                        payment.PaidAt = now;
                        payment.UpdatedAt = now;
                        order.Status = OrderStatus.Completed;
                        order.CompletedAt = now;
                        order.UpdatedAt = now;

                        await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));

                        return new PaymentStatusResponse
                        {
                            OrderId = order.Id,
                            PaymentStatus = PaymentStatus.SUCCESS.ToString(),
                            OrderStatus = OrderStatus.Completed.ToString()
                        };
                    }
                }
            }

            return new PaymentStatusResponse
            {
                OrderId = order.Id,
                PaymentStatus = payment.Status.ToString(),
                OrderStatus = order.Status.ToString()
            };
        }

        public async Task<PaymentStatusResponse> CancelPendingPaymentAsync(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var session = await _orderRepository.GetActiveSessionByCodeAsync(normalizedCode)
                ?? throw new NotFoundException("Session not found or expired");

            if (!session.ActiveOrderId.HasValue)
            {
                throw new BusinessRuleException("No active order found for this session.");
            }

            var order = await _orderRepository.GetOrderWithPaymentsAsync(session.ActiveOrderId.Value)
                ?? throw new NotFoundException("Order not found");

            var payment = order.Payments
                .Where(p => p.Method == PaymentMethod.PAYOS && p.Status == PaymentStatus.PENDING)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (payment is null)
            {
                return new PaymentStatusResponse
                {
                    OrderId = order.Id,
                    PaymentStatus = "NO_PENDING_PAYMENT",
                    OrderStatus = order.Status.ToString()
                };
            }

            var now = DateTime.UtcNow;
            await _orderRepository.MarkPendingPaymentsFailedAsync(order.Id, now);
            await _unitOfWork.SaveChangesAsync();

            payment.Status = PaymentStatus.FAILED;
            payment.UpdatedAt = now;

            return new PaymentStatusResponse
            {
                OrderId = order.Id,
                PaymentStatus = PaymentStatus.FAILED.ToString(),
                OrderStatus = order.Status.ToString()
            };
        }

        private async Task<CheckoutResponse> HandleCashPaymentAsync(Domain.Entities.Order order)
        {
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Method = PaymentMethod.CASH,
                Status = PaymentStatus.PENDING,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var now = DateTime.UtcNow;
            await _orderRepository.AddPaymentAsync(payment);
            await _orderRepository.MarkOrderCompletedAsync(order.Id, now);

            await _unitOfWork.SaveChangesAsync();

            order.Status = OrderStatus.Completed;
            order.CompletedAt = now;
            order.UpdatedAt = now;
            order.Payments.Add(payment);

            await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));

            return new CheckoutResponse
            {
                OrderId = order.Id,
                PaymentId = payment.Id,
                PaymentMethod = PaymentMethod.CASH
            };
        }

        private async Task<CheckoutResponse> HandlePayOSPaymentAsync(Domain.Entities.Order order, QrSession session)
        {
            var config = await _branchSettingsRepository.GetPaymentConfigAsync(order.BranchId);
            if (config is null
                || !config.PayOsEnabled
                || string.IsNullOrWhiteSpace(config.PayOsClientId)
                || string.IsNullOrWhiteSpace(config.PayOsApiKey)
                || string.IsNullOrWhiteSpace(config.PayOsChecksumKey))
            {
                throw new BusinessRuleException("PayOS is not configured for this branch. Please pay at the cashier.");
            }

            var orderCode = GenerateOrderCode(order);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

            var linkResult = await _paymentService.CreatePaymentLinkAsync(new CreatePaymentLinkInput
            {
                OrderCode = orderCode,
                Amount = (long)order.TotalAmount,
                Description = BuildPaymentDescription(orderCode),
                BuyerName = order.CustomerName,
                BuyerPhone = order.CustomerPhone,
                ExpiredAtUnixSeconds = (int)expiresAt.ToUnixTimeSeconds(),
                PayOsClientId = config.PayOsClientId,
                PayOsApiKey = config.PayOsApiKey,
                PayOsChecksumKey = config.PayOsChecksumKey,
                ReturnUrl = BuildPaymentRedirectUrl("return", session.SessionToken, order),
                CancelUrl = BuildPaymentRedirectUrl("cancel", session.SessionToken, order)
            });

            if (!linkResult.Success)
            {
                throw new BusinessRuleException(
                    $"Failed to create payment link: {linkResult.ErrorMessage}");
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Method = PaymentMethod.PAYOS,
                Status = PaymentStatus.PENDING,
                GatewayOrderId = orderCode.ToString(),
                PaymentUrl = linkResult.CheckoutUrl,
                GatewayResponseData = SerializePayOsSnapshot(linkResult, expiresAt.UtcDateTime),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _orderRepository.AddPaymentAsync(payment);
            await _orderRepository.TouchOrderAsync(order.Id, DateTime.UtcNow);

            await _unitOfWork.SaveChangesAsync();

            return new CheckoutResponse
            {
                OrderId = order.Id,
                PaymentId = payment.Id,
                PaymentMethod = PaymentMethod.PAYOS,
                CheckoutUrl = linkResult.CheckoutUrl,
                QrCode = linkResult.QrCode,
                Bin = linkResult.Bin,
                AccountNumber = linkResult.AccountNumber,
                AccountName = linkResult.AccountName,
                Amount = linkResult.Amount,
                Description = linkResult.Description,
                PaymentExpiresAt = expiresAt.UtcDateTime
            };
        }

        private static CheckoutResponse BuildCheckoutResponse(Domain.Entities.Order order, Payment payment)
        {
            var snapshot = DeserializePayOsSnapshot(payment.GatewayResponseData);
            return new CheckoutResponse
            {
                OrderId = order.Id,
                PaymentId = payment.Id,
                PaymentMethod = payment.Method,
                CheckoutUrl = payment.PaymentUrl ?? snapshot?.CheckoutUrl,
                QrCode = snapshot?.QrCode,
                Bin = snapshot?.Bin,
                AccountNumber = snapshot?.AccountNumber,
                AccountName = snapshot?.AccountName,
                Amount = snapshot?.Amount,
                Description = snapshot?.Description,
                PaymentExpiresAt = snapshot?.ExpiresAtUtc
            };
        }

        private static string SerializePayOsSnapshot(PaymentLinkResult linkResult, DateTime expiresAtUtc)
        {
            return JsonSerializer.Serialize(new PayOsPaymentSnapshot
            {
                CheckoutUrl = linkResult.CheckoutUrl,
                QrCode = linkResult.QrCode,
                Bin = linkResult.Bin,
                AccountNumber = linkResult.AccountNumber,
                AccountName = linkResult.AccountName,
                Amount = linkResult.Amount,
                Description = linkResult.Description,
                ExpiresAtUtc = expiresAtUtc
            });
        }

        private static PayOsPaymentSnapshot? DeserializePayOsSnapshot(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<PayOsPaymentSnapshot>(value);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed class PayOsPaymentSnapshot
        {
            public string? CheckoutUrl { get; set; }
            public string? QrCode { get; set; }
            public string? Bin { get; set; }
            public string? AccountNumber { get; set; }
            public string? AccountName { get; set; }
            public long? Amount { get; set; }
            public string? Description { get; set; }
            public DateTime? ExpiresAtUtc { get; set; }
        }

        private static long GenerateOrderCode(Domain.Entities.Order order)
        {
            var hash = Math.Abs(order.Id.GetHashCode());
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
            return (timestamp * 100000) + (hash % 100000);
        }

        private static string BuildPaymentDescription(long orderCode) => $"SN {orderCode}";

        private string BuildPaymentRedirectUrl(string result, string sessionCode, Domain.Entities.Order order)
        {
            var query = $"sessionCode={Uri.EscapeDataString(sessionCode)}&orderId={order.Id}";
            return _urlBuilder.BuildTenantPaymentUrl(order.Branch?.Restaurant?.Slug, result, query);
        }

        private static string? NormalizeUrl(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
        }
    }
}
