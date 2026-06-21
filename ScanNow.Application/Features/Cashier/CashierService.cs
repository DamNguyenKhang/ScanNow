using FluentValidation;
using Microsoft.Extensions.Configuration;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Cashier.DTOs;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Application.Mappers;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;
using System.Text.Json;
using OrderEntity = ScanNow.Domain.Entities.Order;

namespace ScanNow.Application.Features.Cashier
{
    public class CashierService : ICashierService
    {
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private static readonly string BranchManagerRole = UserRole.BRANCH_MANAGER.ToString();
        private static readonly string CashierRole = UserRole.CASHIER.ToString();

        private readonly IOrderRepository _orderRepository;
        private readonly ITableQrRepository _tableRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentService _paymentService;
        private readonly IBranchSettingsRepository _branchSettingsRepository;
        private readonly IOrderUpdatePublisher _publisher;
        private readonly IValidator<CashierOrderQuery> _queryValidator;
        private readonly IValidator<CashierCheckoutRequest> _checkoutValidator;
        private readonly IConfiguration _configuration;
        private readonly ITenantUrlBuilder _urlBuilder;

        public CashierService(
            IOrderRepository orderRepository,
            ITableQrRepository tableRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork,
            IPaymentService paymentService,
            IBranchSettingsRepository branchSettingsRepository,
            IOrderUpdatePublisher publisher,
            IValidator<CashierOrderQuery> queryValidator,
            IValidator<CashierCheckoutRequest> checkoutValidator,
            IConfiguration configuration,
            ITenantUrlBuilder urlBuilder)
        {
            _orderRepository = orderRepository;
            _tableRepository = tableRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _paymentService = paymentService;
            _branchSettingsRepository = branchSettingsRepository;
            _publisher = publisher;
            _queryValidator = queryValidator;
            _checkoutValidator = checkoutValidator;
            _configuration = configuration;
            _urlBuilder = urlBuilder;
        }

        public async Task<PagedResult<TableOrderHistoryResponse>> GetBranchOrdersAsync(Guid branchId, CashierOrderQuery query)
        {
            await _queryValidator.ValidateAndThrowAsync(query);
            await EnsureCanAccessBranchAsync(branchId);

            var orders = await _orderRepository.GetOrdersByBranchAsync(branchId);
            var filteredOrders = ApplyFilters(orders, query).ToList();
            var sortedOrders = ApplySort(filteredOrders, query).ToList();
            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);

            return new PagedResult<TableOrderHistoryResponse>
            {
                Items = sortedOrders
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(MapOrder)
                    .ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = filteredOrders.Count
            };
        }

        public async Task<TableOrderHistoryResponse> GetBranchOrderAsync(Guid branchId, Guid orderId)
        {
            await EnsureCanAccessBranchAsync(branchId);
            var order = await _orderRepository.GetOrderWithDetailsAsync(orderId)
                ?? throw new NotFoundException("Order not found");

            if (order.BranchId != branchId)
            {
                throw new ForbiddenException();
            }

            return MapOrder(order);
        }

        public async Task<CashierPaymentResponse> CheckoutAsync(Guid branchId, Guid orderId, CashierCheckoutRequest request)
        {
            await _checkoutValidator.ValidateAndThrowAsync(request);
            await EnsureCanAccessBranchAsync(branchId);

            var order = await _orderRepository.GetOrderWithDetailsAsync(orderId)
                ?? throw new NotFoundException("Order not found");

            if (order.BranchId != branchId)
            {
                throw new ForbiddenException();
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                throw new BusinessRuleException("Cancelled order cannot be paid.");
            }

            var successfulPayment = GetLatestPayment(order, PaymentStatus.SUCCESS);
            if (successfulPayment is not null)
            {
                throw new ConflictException("This order has already been paid.");
            }

            var pendingPayOsPayment = order.Payments
                .Where(x => x.Method == PaymentMethod.PAYOS && x.Status == PaymentStatus.PENDING)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (pendingPayOsPayment is not null)
            {
                if (request.PaymentMethod == PaymentMethod.CASH)
                {
                    throw new BusinessRuleException("This order has a pending PayOS QR payment. Complete or cancel the QR payment before accepting cash.");
                }

                return BuildPaymentResponse(order, pendingPayOsPayment);
            }

            await ApplyVoucherAsync(order, request.VoucherCode);

            return request.PaymentMethod == PaymentMethod.CASH
                ? await HandleCashAsync(order, request.AmountReceived)
                : await HandlePayOsAsync(order);
        }

        public async Task<TableOrderHistoryResponse> CancelPendingPaymentAsync(Guid branchId, Guid orderId)
        {
            await EnsureCanAccessBranchAsync(branchId);

            var order = await _orderRepository.GetOrderWithDetailsAsync(orderId)
                ?? throw new NotFoundException("Order not found");

            if (order.BranchId != branchId)
            {
                throw new ForbiddenException();
            }

            var pendingPayment = order.Payments
                .Where(x => x.Status == PaymentStatus.PENDING)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefault();

            if (pendingPayment is null)
            {
                throw new BusinessRuleException("This order does not have a pending payment.");
            }

            var now = DateTime.UtcNow;
            await _orderRepository.MarkPendingPaymentsFailedAsync(order.Id, now);
            await _unitOfWork.SaveChangesAsync();

            foreach (var payment in order.Payments.Where(x => x.Status == PaymentStatus.PENDING))
            {
                payment.Status = PaymentStatus.FAILED;
                payment.UpdatedAt = now;
            }

            return MapOrder(order);
        }

        private async Task ApplyVoucherAsync(OrderEntity order, string? voucherCode)
        {
            if (string.IsNullOrWhiteSpace(voucherCode))
            {
                return;
            }

            if (order.DiscountAmount > 0)
            {
                throw new BusinessRuleException("This order already has a discount.");
            }

            var voucher = await _branchSettingsRepository.GetPaperVoucherByCodeAsync(order.BranchId, voucherCode);
            if (voucher is null || !voucher.IsActive)
            {
                throw new NotFoundException("Voucher not found or inactive.");
            }

            var now = DateTime.UtcNow;
            if (voucher.ValidFrom.HasValue && voucher.ValidFrom.Value > now)
            {
                throw new BusinessRuleException("Voucher is not valid yet.");
            }

            if (voucher.ValidUntil.HasValue && voucher.ValidUntil.Value < now)
            {
                throw new BusinessRuleException("Voucher has expired.");
            }

            if (voucher.UsedCount >= voucher.Quantity)
            {
                throw new BusinessRuleException("Voucher has no remaining usage.");
            }

            var grossAmount = order.SubTotal + order.VatAmount + order.ServiceChargeAmount;
            if (grossAmount < voucher.MinOrderAmount)
            {
                throw new BusinessRuleException("Order does not meet voucher minimum amount.");
            }

            var discount = voucher.DiscountType == DiscountType.PERCENT
                ? grossAmount * voucher.DiscountValue / 100m
                : voucher.DiscountValue;

            if (voucher.MaxDiscountAmount.HasValue)
            {
                discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
            }

            discount = Math.Min(discount, grossAmount);
            order.DiscountAmount = discount;
            order.TotalAmount = grossAmount - discount;
            order.UpdatedAt = now;
            voucher.UsedCount += 1;
            voucher.UpdatedAt = now;
        }

        private async Task<CashierPaymentResponse> HandleCashAsync(OrderEntity order, decimal? amountReceived)
        {
            var now = DateTime.UtcNow;
            await _orderRepository.MarkPendingPaymentsFailedAsync(order.Id, now);

            if (!amountReceived.HasValue)
            {
                throw new BusinessRuleException("Amount received is required for cash payment.");
            }

            if (amountReceived.Value < order.TotalAmount)
            {
                throw new BusinessRuleException("Amount received must be greater than or equal to order total.");
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Method = PaymentMethod.CASH,
                Status = PaymentStatus.SUCCESS,
                AmountReceived = amountReceived.Value,
                ChangeAmount = amountReceived.Value - order.TotalAmount,
                PaidAt = now,
                ProcessedById = _currentUserService.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _orderRepository.AddPaymentAsync(payment);
            await _orderRepository.MarkOrderCompletedAsync(order.Id, now);

            await _unitOfWork.SaveChangesAsync();

            order.Status = OrderStatus.Completed;
            order.CompletedAt = now;
            order.UpdatedAt = now;
            if (order.Payments.All(x => x.Id != payment.Id))
            {
                order.Payments.Add(payment);
            }

            await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));

            return BuildPaymentResponse(order, payment);
        }

        private async Task<CashierPaymentResponse> HandlePayOsAsync(OrderEntity order)
        {
            var config = await _branchSettingsRepository.GetPaymentConfigAsync(order.BranchId);
            if (config is null
                || !config.PayOsEnabled
                || string.IsNullOrWhiteSpace(config.PayOsClientId)
                || string.IsNullOrWhiteSpace(config.PayOsApiKey)
                || string.IsNullOrWhiteSpace(config.PayOsChecksumKey))
            {
                throw new BusinessRuleException("PayOS is not configured for this branch. Please use cash payment.");
            }

            var existingPayOs = order.Payments
                .Where(x => x.Method == PaymentMethod.PAYOS && x.Status == PaymentStatus.PENDING)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (existingPayOs is not null)
            {
                return BuildPaymentResponse(order, existingPayOs);
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
                ReturnUrl = BuildCashierPaymentRedirectUrl("return", order),
                CancelUrl = BuildCashierPaymentRedirectUrl("cancel", order)
            });

            if (!linkResult.Success)
            {
                throw new BusinessRuleException($"Failed to create payment link: {linkResult.ErrorMessage}");
            }

            var now = DateTime.UtcNow;
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
                ProcessedById = _currentUserService.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _orderRepository.AddPaymentAsync(payment);
            await _orderRepository.TouchOrderAsync(order.Id, now);
            await _unitOfWork.SaveChangesAsync();

            order.UpdatedAt = now;
            if (order.Payments.All(x => x.Id != payment.Id))
            {
                order.Payments.Add(payment);
            }

            return BuildPaymentResponse(order, payment, linkResult);
        }

        private async Task EnsureCanAccessBranchAsync(Guid branchId)
        {
            var branch = await _tableRepository.GetBranchByIdAsync(branchId)
                ?? throw new NotFoundException("Branch not found");
            var userId = _currentUserService.UserId ?? throw new UnauthorizedException();
            var role = _currentUserService.Role;

            if (role == OwnerRole && branch.Restaurant.OwnerId == userId)
            {
                return;
            }

            if (role == BranchManagerRole && branch.ManagerId == userId)
            {
                return;
            }

            if (role == CashierRole && await _tableRepository.UserBelongsToBranchAsync(userId, branchId))
            {
                return;
            }

            throw new ForbiddenException();
        }

        private static IEnumerable<OrderEntity> ApplyFilters(IEnumerable<OrderEntity> orders, CashierOrderQuery query)
        {
            var status = query.Status?.Trim().ToLowerInvariant();
            if (status == "active" || string.IsNullOrWhiteSpace(status))
            {
                orders = orders.Where(x => x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled);
            }
            else if (status == "paid")
            {
                orders = orders.Where(x => x.Status == OrderStatus.Completed || GetLatestPayment(x, PaymentStatus.SUCCESS) is not null);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                orders = orders.Where(x =>
                    x.OrderNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Table?.TableNumber.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || (x.CustomerName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || (x.CustomerPhone?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || x.QrSessions.Any(session => session.SessionToken.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            return orders;
        }

        private static IEnumerable<OrderEntity> ApplySort(IEnumerable<OrderEntity> orders, CashierOrderQuery query)
        {
            var desc = query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
            return (query.SortBy ?? "createdAt").Trim().ToLowerInvariant() switch
            {
                "tablenumber" => desc ? orders.OrderByDescending(x => x.Table?.TableNumber) : orders.OrderBy(x => x.Table?.TableNumber),
                "totalamount" => desc ? orders.OrderByDescending(x => x.TotalAmount) : orders.OrderBy(x => x.TotalAmount),
                "status" => desc ? orders.OrderByDescending(x => x.Status) : orders.OrderBy(x => x.Status),
                _ => desc ? orders.OrderByDescending(x => x.CreatedAt) : orders.OrderBy(x => x.CreatedAt)
            };
        }

        private static Payment? GetLatestPayment(OrderEntity order, PaymentStatus? status = null)
        {
            var payments = status.HasValue
                ? order.Payments.Where(x => x.Status == status.Value)
                : order.Payments;

            return payments
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefault();
        }

        private static CashierPaymentResponse BuildPaymentResponse(OrderEntity order, Payment payment, PaymentLinkResult? linkResult = null)
        {
            var snapshot = DeserializePayOsSnapshot(payment.GatewayResponseData);
            return new CashierPaymentResponse
            {
                OrderId = order.Id,
                PaymentId = payment.Id,
                PaymentMethod = payment.Method,
                PaymentStatus = payment.Status,
                OrderStatus = order.Status.ToString(),
                CheckoutUrl = payment.PaymentUrl ?? linkResult?.CheckoutUrl ?? snapshot?.CheckoutUrl,
                QrCode = linkResult?.QrCode ?? snapshot?.QrCode,
                Bin = linkResult?.Bin ?? snapshot?.Bin,
                AccountNumber = linkResult?.AccountNumber ?? snapshot?.AccountNumber,
                AccountName = linkResult?.AccountName ?? snapshot?.AccountName,
                Amount = linkResult?.Amount ?? snapshot?.Amount,
                AmountReceived = payment.AmountReceived,
                ChangeAmount = payment.ChangeAmount,
                Description = linkResult?.Description ?? snapshot?.Description,
                PaymentExpiresAt = snapshot?.ExpiresAtUtc,
                Order = MapOrder(order)
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

        private static TableOrderHistoryResponse MapOrder(OrderEntity order)
        {
            var latestPayment = GetLatestPayment(order);
            return new TableOrderHistoryResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                BranchId = order.BranchId,
                TableId = order.TableId,
                TableNumber = order.Table?.TableNumber,
                SessionCode = order.QrSessions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.SessionToken,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerNote = order.CustomerNote,
                SubTotal = order.SubTotal,
                VatAmount = order.VatAmount,
                ServiceChargeAmount = order.ServiceChargeAmount,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                PaymentMethod = latestPayment?.Method.ToString(),
                PaymentStatus = latestPayment?.Status.ToString(),
                AmountReceived = latestPayment?.AmountReceived,
                ChangeAmount = latestPayment?.ChangeAmount,
                PaidAt = latestPayment?.PaidAt,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Items = order.Items
                    .OrderBy(x => x.CreatedAt)
                    .Select(item => new CustomerOrderItemResponse
                    {
                        OrderItemId = item.Id,
                        MenuItemId = item.MenuItemId,
                        MenuItemName = item.MenuItemName,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        SubTotal = item.SubTotal,
                        Note = item.Note,
                        Status = item.Status,
                        EstimatedCookingMinutes = item.EstimatedCookingMinutes
                    })
                    .ToList()
            };
        }

        private static long GenerateOrderCode(OrderEntity order)
        {
            var hash = Math.Abs(order.Id.GetHashCode());
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
            return (timestamp * 100000) + (hash % 100000);
        }

        private static string BuildPaymentDescription(long orderCode) => $"SN {orderCode}";

        private string BuildCashierPaymentRedirectUrl(string result, OrderEntity order)
        {
            var query = $"orderId={order.Id}&source=cashier";
            return _urlBuilder.BuildTenantPaymentUrl(order.Branch?.Restaurant?.Slug, result, query);
        }

        private static string? NormalizeUrl(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
        }
    }
}
