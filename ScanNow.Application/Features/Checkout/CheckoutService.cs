using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Checkout.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.Checkout
{
    public class CheckoutService : ICheckoutService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentService _paymentService;
        private readonly IValidator<CreateCheckoutRequest> _checkoutValidator;
        private readonly IOrderUpdatePublisher _publisher;

        public CheckoutService(
            IOrderRepository orderRepository,
            IUnitOfWork unitOfWork,
            IPaymentService paymentService,
            IValidator<CreateCheckoutRequest> checkoutValidator,
            IOrderUpdatePublisher publisher)
        {
            _orderRepository = orderRepository;
            _unitOfWork = unitOfWork;
            _paymentService = paymentService;
            _checkoutValidator = checkoutValidator;
            _publisher = publisher;
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

            var order = await _orderRepository.GetOrderWithPaymentsAsync(session.ActiveOrderId.Value)
                ?? throw new NotFoundException("Order not found");

            if (order.Status == OrderStatus.Completed)
            {
                throw new ConflictException($"Order is already in '{order.Status}' status and cannot be checked out.");
            }

            var existingPayment = order.Payments.FirstOrDefault(p =>
                p.Status == PaymentStatus.SUCCESS || p.Status == PaymentStatus.PENDING);

            if (existingPayment is { Status: PaymentStatus.SUCCESS })
            {
                throw new ConflictException("This order has already been paid.");
            }

            if (existingPayment is { Status: PaymentStatus.PENDING, Method: PaymentMethod.PAYOS })
            {
                return new CheckoutResponse
                {
                    OrderId = order.Id,
                    PaymentId = existingPayment.Id,
                    PaymentMethod = PaymentMethod.PAYOS,
                    CheckoutUrl = existingPayment.PaymentUrl
                };
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
                    var gatewayResult = await _paymentService.GetPaymentStatusAsync(orderCode);

                    if (gatewayResult.IsPaid)
                    {
                        payment.Status = PaymentStatus.SUCCESS;
                        payment.TransactionId = gatewayResult.TransactionId;
                        payment.PaidAt = DateTime.UtcNow;
                        payment.UpdatedAt = DateTime.UtcNow;

                        order.Status = OrderStatus.Completed;
                        order.CompletedAt = DateTime.UtcNow;
                        order.UpdatedAt = DateTime.UtcNow;

                        await _unitOfWork.SaveChangesAsync();
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

            order.Payments.Add(payment);
            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
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
            var orderCode = GenerateOrderCode(order);

            var linkResult = await _paymentService.CreatePaymentLinkAsync(new CreatePaymentLinkInput
            {
                OrderCode = orderCode,
                Amount = (long)order.TotalAmount,
                Description = $"SN {order.OrderNumber}",
                BuyerName = order.CustomerName,
                BuyerPhone = order.CustomerPhone,
                ExpiredAtUnixSeconds = (int)DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds()
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            order.Payments.Add(payment);
            order.UpdatedAt = DateTime.UtcNow;

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
                Description = linkResult.Description
            };
        }

        private static long GenerateOrderCode(Domain.Entities.Order order)
        {
            var hash = Math.Abs(order.Id.GetHashCode());
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
            return (timestamp * 100000) + (hash % 100000);
        }
    }
}
