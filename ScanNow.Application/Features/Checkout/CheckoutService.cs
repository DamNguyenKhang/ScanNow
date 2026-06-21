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

            // Collect ALL active (non-Cancelled, non-Completed) orders for this session.
            var activeOrders = await _orderRepository.GetActiveOrdersBySessionCodeAsync(normalizedCode);

            if (activeOrders.Count == 0)
            {
                throw new BusinessRuleException("No active orders found for this session. Please place an order first.");
            }

            // Check if any order is already fully paid.
            var anyAlreadyPaid = activeOrders.Any(o =>
                o.Payments.Any(p => p.Status == PaymentStatus.SUCCESS));
            if (anyAlreadyPaid)
            {
                throw new ConflictException("One or more orders in this session have already been paid.");
            }

            // Aggregate totals across all active orders for the payment amount.
            var aggregatedTotal = activeOrders.Sum(o => o.TotalAmount);

            // If a PENDING PayOS payment already exists across any order, reuse it ONLY if the amount matches.
            var pendingPayOsPayment = activeOrders
                .SelectMany(o => o.Payments)
                .Where(p => p.Status == PaymentStatus.PENDING && p.Method == PaymentMethod.PAYOS)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .FirstOrDefault();

            if (pendingPayOsPayment != null)
            {
                if (pendingPayOsPayment.Amount == aggregatedTotal)
                {
                    // Use the first order for the response context (any order works since they share 1 payment).
                    var representativeOrder = activeOrders.First(o => o.Payments.Any(p => p.Id == pendingPayOsPayment.Id));
                    return BuildCheckoutResponse(representativeOrder, pendingPayOsPayment);
                }
                else
                {
                    // The cart total has changed since the payment was generated.
                    // Mark the old pending payment as FAILED so we can generate a new one.
                    pendingPayOsPayment.Status = PaymentStatus.FAILED;
                    pendingPayOsPayment.UpdatedAt = DateTime.UtcNow;
                    // Note: We don't save immediately, we'll let the unit of work save at the end of HandlePayOSPaymentAsync
                }
            }

            // We attach the payment to the first (oldest) order for record-keeping.
            var primaryOrder = activeOrders.First();
            // Load Branch + Restaurant for URL building (the AsNoTracking result may not have them).
            var primaryOrderWithDetails = await _orderRepository.GetOrderWithDetailsAsync(primaryOrder.Id)
                ?? throw new NotFoundException("Primary order not found");

            if (request.PaymentMethod == PaymentMethod.CASH)
            {
                return await HandleCashPaymentAsync(primaryOrderWithDetails, activeOrders, aggregatedTotal);
            }

            return await HandlePayOSPaymentAsync(primaryOrderWithDetails, session, activeOrders, aggregatedTotal);
        }

        public async Task<PaymentStatusResponse> GetPaymentStatusAsync(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var session = await _orderRepository.GetActiveSessionByCodeAsync(normalizedCode)
                ?? throw new NotFoundException("Session not found or expired");

            // Look across all active orders for a PayOS payment.
            var activeOrders = await _orderRepository.GetActiveOrdersBySessionCodeAsync(normalizedCode);

            if (activeOrders.Count == 0)
            {
                throw new BusinessRuleException("No active orders found for this session.");
            }

            var payment = activeOrders
                .SelectMany(o => o.Payments)
                .Where(p => p.Method == PaymentMethod.PAYOS)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            var primaryOrder = activeOrders.First();

            if (payment == null)
            {
                return new PaymentStatusResponse
                {
                    OrderId = primaryOrder.Id,
                    PaymentStatus = "NO_PAYMENT",
                    OrderStatus = primaryOrder.Status.ToString()
                };
            }

            if (payment.Status == PaymentStatus.SUCCESS)
            {
                return new PaymentStatusResponse
                {
                    OrderId = primaryOrder.Id,
                    PaymentStatus = PaymentStatus.SUCCESS.ToString(),
                    OrderStatus = primaryOrder.Status.ToString()
                };
            }

            if (payment.Status == PaymentStatus.PENDING && !string.IsNullOrEmpty(payment.GatewayOrderId))
            {
                if (long.TryParse(payment.GatewayOrderId, out var orderCode))
                {
                    var config = await _branchSettingsRepository.GetPaymentConfigAsync(primaryOrder.BranchId);
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
                        var orderIds = activeOrders.Select(o => o.Id).ToList();

                        await _orderRepository.MarkPaymentSucceededAsync(payment.Id, gatewayResult.TransactionId, now);
                        await _orderRepository.MarkOrdersCompletedAsync(orderIds, now);
                        await _unitOfWork.SaveChangesAsync();

                        // Publish updates for every completed order.
                        foreach (var o in activeOrders)
                        {
                            o.Status = OrderStatus.Completed;
                            o.CompletedAt = now;
                            o.UpdatedAt = now;
                        }
                        payment.Status = PaymentStatus.SUCCESS;

                        foreach (var o in activeOrders)
                        {
                            await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(o));
                        }

                        return new PaymentStatusResponse
                        {
                            OrderId = primaryOrder.Id,
                            PaymentStatus = PaymentStatus.SUCCESS.ToString(),
                            OrderStatus = OrderStatus.Completed.ToString()
                        };
                    }
                }
            }

            return new PaymentStatusResponse
            {
                OrderId = primaryOrder.Id,
                PaymentStatus = payment.Status.ToString(),
                OrderStatus = primaryOrder.Status.ToString()
            };
        }

        public async Task<PaymentStatusResponse> CancelPendingPaymentAsync(string sessionCode)
        {
            var normalizedCode = sessionCode.Trim().ToUpperInvariant();
            var session = await _orderRepository.GetActiveSessionByCodeAsync(normalizedCode)
                ?? throw new NotFoundException("Session not found or expired");

            var activeOrders = await _orderRepository.GetActiveOrdersBySessionCodeAsync(normalizedCode);

            if (activeOrders.Count == 0)
            {
                throw new BusinessRuleException("No active orders found for this session.");
            }

            var primaryOrder = activeOrders.First();

            // Find the latest pending PayOS payment across all orders.
            var payment = activeOrders
                .SelectMany(o => o.Payments)
                .Where(p => p.Method == PaymentMethod.PAYOS && p.Status == PaymentStatus.PENDING)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (payment is null)
            {
                return new PaymentStatusResponse
                {
                    OrderId = primaryOrder.Id,
                    PaymentStatus = "NO_PENDING_PAYMENT",
                    OrderStatus = primaryOrder.Status.ToString()
                };
            }

            var now = DateTime.UtcNow;
            // Cancel pending payments for all orders in the session.
            foreach (var o in activeOrders)
            {
                await _orderRepository.MarkPendingPaymentsFailedAsync(o.Id, now);
            }
            await _unitOfWork.SaveChangesAsync();

            payment.Status = PaymentStatus.FAILED;
            payment.UpdatedAt = now;

            return new PaymentStatusResponse
            {
                OrderId = primaryOrder.Id,
                PaymentStatus = PaymentStatus.FAILED.ToString(),
                OrderStatus = primaryOrder.Status.ToString()
            };
        }

        private async Task<CheckoutResponse> HandleCashPaymentAsync(
            Domain.Entities.Order primaryOrder,
            List<Domain.Entities.Order> allOrders,
            decimal aggregatedTotal)
        {
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = primaryOrder.Id,
                Amount = aggregatedTotal,
                Method = PaymentMethod.CASH,
                Status = PaymentStatus.PENDING,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var now = DateTime.UtcNow;
            var orderIds = allOrders.Select(o => o.Id).ToList();

            await _orderRepository.AddPaymentAsync(payment);
            await _orderRepository.MarkOrdersCompletedAsync(orderIds, now);

            await _unitOfWork.SaveChangesAsync();

            // Publish completion for every order.
            foreach (var o in allOrders)
            {
                o.Status = OrderStatus.Completed;
                o.CompletedAt = now;
                o.UpdatedAt = now;
                await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(o));
            }

            return new CheckoutResponse
            {
                OrderId = primaryOrder.Id,
                PaymentId = payment.Id,
                PaymentMethod = PaymentMethod.CASH
            };
        }

        private async Task<CheckoutResponse> HandlePayOSPaymentAsync(
            Domain.Entities.Order primaryOrder,
            QrSession session,
            List<Domain.Entities.Order> allOrders,
            decimal aggregatedTotal)
        {
            var config = await _branchSettingsRepository.GetPaymentConfigAsync(primaryOrder.BranchId);
            if (config is null
                || !config.PayOsEnabled
                || string.IsNullOrWhiteSpace(config.PayOsClientId)
                || string.IsNullOrWhiteSpace(config.PayOsApiKey)
                || string.IsNullOrWhiteSpace(config.PayOsChecksumKey))
            {
                throw new BusinessRuleException("PayOS is not configured for this branch. Please pay at the cashier.");
            }

            var orderCode = GenerateOrderCode(primaryOrder);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

            var linkResult = await _paymentService.CreatePaymentLinkAsync(new CreatePaymentLinkInput
            {
                OrderCode = orderCode,
                Amount = (long)aggregatedTotal,          // aggregated total for all orders
                Description = BuildPaymentDescription(orderCode),
                BuyerName = primaryOrder.CustomerName,
                BuyerPhone = primaryOrder.CustomerPhone,
                ExpiredAtUnixSeconds = (int)expiresAt.ToUnixTimeSeconds(),
                PayOsClientId = config.PayOsClientId,
                PayOsApiKey = config.PayOsApiKey,
                PayOsChecksumKey = config.PayOsChecksumKey,
                ReturnUrl = BuildPaymentRedirectUrl("return", session.SessionToken, primaryOrder),
                CancelUrl = BuildPaymentRedirectUrl("cancel", session.SessionToken, primaryOrder)
            });

            if (!linkResult.Success)
            {
                throw new BusinessRuleException(
                    $"Failed to create payment link: {linkResult.ErrorMessage}");
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = primaryOrder.Id,
                Amount = aggregatedTotal,
                Method = PaymentMethod.PAYOS,
                Status = PaymentStatus.PENDING,
                GatewayOrderId = orderCode.ToString(),
                PaymentUrl = linkResult.CheckoutUrl,
                GatewayResponseData = SerializePayOsSnapshot(linkResult, expiresAt.UtcDateTime),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _orderRepository.AddPaymentAsync(payment);
            // Touch all active orders so the gateway order code can be correlated back.
            foreach (var o in allOrders)
            {
                await _orderRepository.TouchOrderAsync(o.Id, DateTime.UtcNow);
            }

            await _unitOfWork.SaveChangesAsync();

            return new CheckoutResponse
            {
                OrderId = primaryOrder.Id,
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
