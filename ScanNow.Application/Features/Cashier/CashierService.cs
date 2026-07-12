using FluentValidation;
using Microsoft.Extensions.Configuration;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Cashier.DTOs;
using ScanNow.Application.Features.Checkout.DTOs;
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
            var sessions = await _orderRepository.GetSessionsByBranchAsync(branchId);
            var bills = BuildCashierBillContexts(orders, sessions);
            var filteredBills = ApplyFilters(bills, query).ToList();
            var sortedBills = ApplySort(filteredBills, query).ToList();
            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);

            return new PagedResult<TableOrderHistoryResponse>
            {
                Items = sortedBills
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(bill => BuildOrderHistoryResponse(bill, hideOrderNumber: true))
                    .ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = filteredBills.Count
            };
        }

        public async Task<TableOrderHistoryResponse> GetBranchOrderAsync(Guid branchId, Guid orderId)
        {
            var bill = await ResolveBillContextAsync(branchId, orderId, includeClosedOrders: true);
            return BuildOrderHistoryResponse(bill);
        }

        public async Task<CashierBillResponse> GetBillAsync(Guid branchId, Guid orderId)
        {
            var bill = await ResolveBillContextAsync(branchId, orderId);
            return BuildBillResponse(bill);
        }

        public async Task<CashierPaymentResponse> CheckoutAsync(Guid branchId, Guid orderId, CashierCheckoutRequest request)
        {
            await _checkoutValidator.ValidateAndThrowAsync(request);

            var bill = await ResolveBillContextAsync(branchId, orderId);

            if (bill.Orders.Any(x => x.Status == OrderStatus.Cancelled))
            {
                throw new BusinessRuleException("Cancelled order cannot be paid.");
            }

            if (bill.Orders.Any(x => x.Status == OrderStatus.Completed))
            {
                throw new BusinessRuleException("Completed order cannot be paid.");
            }

            var successfulPayment = GetLatestPayment(bill.Orders, PaymentStatus.SUCCESS);
            if (successfulPayment is not null)
            {
                throw new ConflictException("One or more orders in this bill have already been paid.");
            }

            var pendingPayOsPayment = GetLatestPayment(bill.Orders, PaymentStatus.PENDING, PaymentMethod.PAYOS);
            if (pendingPayOsPayment is not null && request.PaymentMethod == PaymentMethod.CASH)
            {
                throw new BusinessRuleException("This bill has a pending PayOS QR payment. Complete or cancel the QR payment before accepting cash.");
            }

            await ApplyVoucherAsync(bill, request.VoucherCode);

            return request.PaymentMethod == PaymentMethod.CASH
                ? await HandleCashAsync(bill, request.AmountReceived)
                : await HandlePayOsAsync(bill);
        }

        public async Task<PaymentStatusResponse> GetBillPaymentStatusAsync(Guid branchId, Guid orderId)
        {
            var bill = await ResolveBillContextAsync(branchId, orderId, includeClosedOrders: true);
            var primaryOrder = bill.PrimaryOrder;
            var payment = GetLatestPayment(bill.Orders, method: PaymentMethod.PAYOS);

            if (payment is null)
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

            if (payment.Status == PaymentStatus.PENDING && !string.IsNullOrWhiteSpace(payment.GatewayOrderId))
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
                        var snapshot = DeserializePayOsSnapshot(payment.GatewayResponseData);
                        var coveredOrderIds = GetCoveredOrderIds(snapshot, bill).ToList();

                        await _orderRepository.MarkPaymentSucceededAsync(payment.Id, gatewayResult.TransactionId, now);
                        await _orderRepository.MarkOrdersCompletedAsync(coveredOrderIds, now);
                        await _unitOfWork.SaveChangesAsync();

                        payment.Status = PaymentStatus.SUCCESS;
                        payment.TransactionId = gatewayResult.TransactionId;
                        payment.PaidAt = now;
                        payment.UpdatedAt = now;

                        var affectedOrders = bill.Orders.Where(x => coveredOrderIds.Contains(x.Id)).ToList();
                        foreach (var order in affectedOrders)
                        {
                            order.Status = OrderStatus.Completed;
                            order.CompletedAt = now;
                            order.UpdatedAt = now;
                        }

                        await PublishOrderUpdatesAsync(affectedOrders);

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

        public async Task<CashierBillResponse> CancelBillPendingPaymentAsync(Guid branchId, Guid orderId)
        {
            var bill = await ResolveBillContextAsync(branchId, orderId);
            var pendingPayment = GetLatestPayment(bill.Orders, PaymentStatus.PENDING);

            if (pendingPayment is null)
            {
                throw new BusinessRuleException("This bill does not have a pending payment.");
            }

            var now = DateTime.UtcNow;
            await FailPendingPaymentsAsync(bill, now);
            await _unitOfWork.SaveChangesAsync();

            return BuildBillResponse(bill);
        }

        public async Task<TableOrderHistoryResponse> CancelPendingPaymentAsync(Guid branchId, Guid orderId)
        {
            var bill = await CancelBillPendingPaymentAsync(branchId, orderId);
            return bill.Orders.FirstOrDefault(x => x.OrderId == orderId) ?? bill.Orders.First();
        }

        private async Task<CashierBillContext> ResolveBillContextAsync(Guid branchId, Guid orderId, bool includeClosedOrders = false)
        {
            await EnsureCanAccessBranchAsync(branchId);

            var orders = await _orderRepository.GetCashierBillOrdersByOrderIdAsync(branchId, orderId, includeClosedOrders);
            if (orders.Count == 0)
            {
                var order = await _orderRepository.GetOrderWithDetailsAsync(orderId)
                    ?? throw new NotFoundException("Order not found");

                if (order.BranchId != branchId)
                {
                    throw new ForbiddenException();
                }

                orders.Add(order);
            }

            var sessions = await _orderRepository.GetSessionsByBranchAsync(branchId);
            var session = orders
                .Select(order => FindSessionForOrder(order, sessions))
                .FirstOrDefault(x => x is not null);

            return new CashierBillContext(orders, session?.SessionToken);
        }

        private async Task ApplyVoucherAsync(CashierBillContext bill, string? voucherCode)
        {
            if (string.IsNullOrWhiteSpace(voucherCode))
            {
                return;
            }

            if (bill.Orders.Any(x => x.DiscountAmount > 0))
            {
                throw new BusinessRuleException("This bill already has a discount.");
            }

            var voucher = await _branchSettingsRepository.GetPaperVoucherByCodeAsync(bill.PrimaryOrder.BranchId, voucherCode);
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

            var grossAmount = bill.Orders.Sum(GetOrderGrossAmount);
            if (grossAmount < voucher.MinOrderAmount)
            {
                throw new BusinessRuleException("Bill does not meet voucher minimum amount.");
            }

            var discount = voucher.DiscountType == DiscountType.PERCENT
                ? grossAmount * voucher.DiscountValue / 100m
                : voucher.DiscountValue;

            if (voucher.MaxDiscountAmount.HasValue)
            {
                discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
            }

            discount = Math.Min(discount, grossAmount);

            var remainingDiscount = discount;
            foreach (var order in bill.Orders)
            {
                var orderGrossAmount = GetOrderGrossAmount(order);
                var appliedDiscount = Math.Min(remainingDiscount, orderGrossAmount);
                order.DiscountAmount = appliedDiscount;
                order.TotalAmount = orderGrossAmount - appliedDiscount;
                order.UpdatedAt = now;
                remainingDiscount -= appliedDiscount;

                if (remainingDiscount <= 0)
                {
                    break;
                }
            }

            voucher.UsedCount += 1;
            voucher.UpdatedAt = now;
        }

        private async Task<CashierPaymentResponse> HandleCashAsync(CashierBillContext bill, decimal? amountReceived)
        {
            if (!amountReceived.HasValue)
            {
                throw new BusinessRuleException("Amount received is required for cash payment.");
            }

            if (amountReceived.Value < bill.TotalAmount)
            {
                throw new BusinessRuleException("Amount received must be greater than or equal to bill total.");
            }

            var now = DateTime.UtcNow;
            await FailPendingPaymentsAsync(bill, now);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = bill.PrimaryOrder.Id,
                Amount = bill.TotalAmount,
                Method = PaymentMethod.CASH,
                Status = PaymentStatus.SUCCESS,
                AmountReceived = amountReceived.Value,
                ChangeAmount = amountReceived.Value - bill.TotalAmount,
                PaidAt = now,
                ProcessedById = _currentUserService.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _orderRepository.AddPaymentAsync(payment);
            await _orderRepository.MarkOrdersCompletedAsync(bill.OrderIds, now);

            await _unitOfWork.SaveChangesAsync();

            foreach (var order in bill.Orders)
            {
                order.Status = OrderStatus.Completed;
                order.CompletedAt = now;
                order.UpdatedAt = now;
            }

            if (bill.PrimaryOrder.Payments.All(x => x.Id != payment.Id))
            {
                bill.PrimaryOrder.Payments.Add(payment);
            }

            await PublishOrderUpdatesAsync(bill.Orders);

            return BuildPaymentResponse(bill, payment);
        }

        private async Task<CashierPaymentResponse> HandlePayOsAsync(CashierBillContext bill)
        {
            var primaryOrder = bill.PrimaryOrder;
            var config = await _branchSettingsRepository.GetPaymentConfigAsync(primaryOrder.BranchId);
            if (config is null
                || !config.PayOsEnabled
                || string.IsNullOrWhiteSpace(config.PayOsClientId)
                || string.IsNullOrWhiteSpace(config.PayOsApiKey)
                || string.IsNullOrWhiteSpace(config.PayOsChecksumKey))
            {
                throw new BusinessRuleException("PayOS is not configured for this branch. Please use cash payment.");
            }

            var existingPayOs = GetLatestPayment(bill.Orders, PaymentStatus.PENDING, PaymentMethod.PAYOS);

            if (existingPayOs is not null)
            {
                if (existingPayOs.Amount == bill.TotalAmount)
                {
                    return BuildPaymentResponse(bill, existingPayOs);
                }

                await FailPendingPaymentsAsync(bill, DateTime.UtcNow);
            }

            var orderCode = GenerateOrderCode(primaryOrder);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
            var linkResult = await _paymentService.CreatePaymentLinkAsync(new CreatePaymentLinkInput
            {
                OrderCode = orderCode,
                Amount = (long)bill.TotalAmount,
                Description = BuildPaymentDescription(orderCode),
                BuyerName = primaryOrder.CustomerName,
                BuyerPhone = primaryOrder.CustomerPhone,
                ExpiredAtUnixSeconds = (int)expiresAt.ToUnixTimeSeconds(),
                PayOsClientId = config.PayOsClientId,
                PayOsApiKey = config.PayOsApiKey,
                PayOsChecksumKey = config.PayOsChecksumKey,
                ReturnUrl = BuildCashierPaymentRedirectUrl("return", primaryOrder),
                CancelUrl = BuildCashierPaymentRedirectUrl("cancel", primaryOrder)
            });

            if (!linkResult.Success)
            {
                throw new BusinessRuleException($"Failed to create payment link: {linkResult.ErrorMessage}");
            }

            var now = DateTime.UtcNow;
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = primaryOrder.Id,
                Amount = bill.TotalAmount,
                Method = PaymentMethod.PAYOS,
                Status = PaymentStatus.PENDING,
                GatewayOrderId = orderCode.ToString(),
                PaymentUrl = linkResult.CheckoutUrl,
                GatewayResponseData = SerializePayOsSnapshot(linkResult, expiresAt.UtcDateTime, bill),
                ProcessedById = _currentUserService.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _orderRepository.AddPaymentAsync(payment);
            foreach (var orderId in bill.OrderIds)
            {
                await _orderRepository.TouchOrderAsync(orderId, now);
            }
            await _unitOfWork.SaveChangesAsync();

            foreach (var order in bill.Orders)
            {
                order.UpdatedAt = now;
            }

            if (primaryOrder.Payments.All(x => x.Id != payment.Id))
            {
                primaryOrder.Payments.Add(payment);
            }

            return BuildPaymentResponse(bill, payment, linkResult);
        }

        private async Task FailPendingPaymentsAsync(CashierBillContext bill, DateTime updatedAt)
        {
            foreach (var order in bill.Orders)
            {
                await _orderRepository.MarkPendingPaymentsFailedAsync(order.Id, updatedAt);
            }

            foreach (var payment in bill.Orders.SelectMany(x => x.Payments).Where(x => x.Status == PaymentStatus.PENDING))
            {
                payment.Status = PaymentStatus.FAILED;
                payment.UpdatedAt = updatedAt;
            }
        }

        private async Task PublishOrderUpdatesAsync(IEnumerable<OrderEntity> orders)
        {
            foreach (var order in orders)
            {
                await _publisher.PublishOrderUpdatedAsync(CustomerOrderMapper.Map(order));
            }
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

        private static IEnumerable<CashierBillContext> ApplyFilters(IEnumerable<CashierBillContext> bills, CashierOrderQuery query)
        {
            var status = query.Status?.Trim().ToLowerInvariant();
            if (status == "active" || string.IsNullOrWhiteSpace(status))
            {
                bills = bills.Where(x => x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled);
            }
            else if (status == "paid")
            {
                bills = bills.Where(x => x.Status == OrderStatus.Completed || GetLatestPayment(x.Orders, PaymentStatus.SUCCESS) is not null);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                bills = bills.Where(bill =>
                    (bill.SessionCode?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || bill.Orders.Any(x =>
                        x.OrderNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (x.Table?.TableNumber.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                        || (x.CustomerName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                        || (x.CustomerPhone?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)));
            }

            return bills;
        }

        private static IEnumerable<CashierBillContext> ApplySort(IEnumerable<CashierBillContext> bills, CashierOrderQuery query)
        {
            var desc = query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
            return (query.SortBy ?? "createdAt").Trim().ToLowerInvariant() switch
            {
                "tablenumber" => desc ? bills.OrderByDescending(x => x.TableNumber) : bills.OrderBy(x => x.TableNumber),
                "totalamount" => desc ? bills.OrderByDescending(x => x.TotalAmount) : bills.OrderBy(x => x.TotalAmount),
                "status" => desc ? bills.OrderByDescending(x => x.Status) : bills.OrderBy(x => x.Status),
                _ => desc ? bills.OrderByDescending(x => x.LastOrderCreatedAt) : bills.OrderBy(x => x.LastOrderCreatedAt)
            };
        }

        private static Payment? GetLatestPayment(OrderEntity order, PaymentStatus? status = null)
        {
            return GetLatestPayment(new[] { order }, status);
        }

        private static Payment? GetLatestPayment(IEnumerable<OrderEntity> orders, PaymentStatus? status = null, PaymentMethod? method = null)
        {
            var payments = orders.SelectMany(x => x.Payments);

            if (status.HasValue)
            {
                payments = payments.Where(x => x.Status == status.Value);
            }

            if (method.HasValue)
            {
                payments = payments.Where(x => x.Method == method.Value);
            }

            return payments
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefault();
        }

        private static CashierBillResponse BuildBillResponse(CashierBillContext bill)
        {
            var latestPayment = GetLatestPayment(bill.Orders);
            return new CashierBillResponse
            {
                PrimaryOrderId = bill.PrimaryOrder.Id,
                SessionCode = bill.SessionCode,
                IsGroupedBill = bill.IsGroupedBill,
                OrderIds = bill.OrderIds,
                SubTotal = bill.SubTotal,
                VatAmount = bill.VatAmount,
                ServiceChargeAmount = bill.ServiceChargeAmount,
                DiscountAmount = bill.DiscountAmount,
                TotalAmount = bill.TotalAmount,
                PaymentId = latestPayment?.Id,
                PaymentMethod = latestPayment?.Method,
                PaymentStatus = latestPayment?.Status,
                AmountReceived = latestPayment?.AmountReceived,
                ChangeAmount = latestPayment?.ChangeAmount,
                PaidAt = latestPayment?.PaidAt,
                Orders = bill.Orders.Select(order => MapOrder(order, bill.SessionCode)).ToList()
            };
        }

        private static TableOrderHistoryResponse BuildOrderHistoryResponse(CashierBillContext bill, bool hideOrderNumber = false)
        {
            var latestPayment = GetLatestPayment(bill.Orders);
            var primaryOrder = bill.PrimaryOrder;

            return new TableOrderHistoryResponse
            {
                OrderId = primaryOrder.Id,
                PrimaryOrderId = primaryOrder.Id,
                IsGroupedBill = bill.IsGroupedBill,
                OrderIds = bill.OrderIds,
                OrderNumber = hideOrderNumber ? string.Empty : primaryOrder.OrderNumber,
                BranchId = primaryOrder.BranchId,
                TableId = primaryOrder.TableId,
                TableNumber = bill.TableNumber,
                SessionCode = bill.SessionCode,
                CustomerName = primaryOrder.CustomerName,
                CustomerPhone = primaryOrder.CustomerPhone,
                CustomerNote = BuildGroupedCustomerNote(bill),
                SubTotal = bill.SubTotal,
                VatAmount = bill.VatAmount,
                ServiceChargeAmount = bill.ServiceChargeAmount,
                DiscountAmount = bill.DiscountAmount,
                TotalAmount = bill.TotalAmount,
                Status = bill.Status,
                PaymentMethod = latestPayment?.Method.ToString(),
                PaymentStatus = latestPayment?.Status.ToString(),
                AmountReceived = latestPayment?.AmountReceived,
                ChangeAmount = latestPayment?.ChangeAmount,
                PaidAt = latestPayment?.PaidAt,
                CreatedAt = bill.FirstOrderCreatedAt,
                UpdatedAt = bill.LastUpdatedAt,
                Items = BuildGroupedItems(bill.Orders),
                Orders = bill.Orders.Select(order => MapOrder(order, bill.SessionCode)).ToList()
            };
        }

        private static CashierPaymentResponse BuildPaymentResponse(CashierBillContext bill, Payment payment, PaymentLinkResult? linkResult = null)
        {
            var snapshot = DeserializePayOsSnapshot(payment.GatewayResponseData);
            var order = bill.PrimaryOrder;
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
                Amount = linkResult?.Amount ?? snapshot?.Amount ?? (payment.Amount > 0 ? (long?)payment.Amount : null),
                AmountReceived = payment.AmountReceived,
                ChangeAmount = payment.ChangeAmount,
                Description = linkResult?.Description ?? snapshot?.Description,
                PaymentExpiresAt = snapshot?.ExpiresAtUtc,
                Order = BuildOrderHistoryResponse(bill),
                Bill = BuildBillResponse(bill)
            };
        }

        private static string SerializePayOsSnapshot(PaymentLinkResult linkResult, DateTime expiresAtUtc, CashierBillContext bill)
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
                ExpiresAtUtc = expiresAtUtc,
                CoveredOrderIds = bill.OrderIds.ToArray(),
                SessionCode = bill.SessionCode
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
            public Guid[]? CoveredOrderIds { get; set; }
            public string? SessionCode { get; set; }
        }

        private sealed class CashierBillContext
        {
            public CashierBillContext(IEnumerable<OrderEntity> orders, string? sessionCode = null)
            {
                Orders = orders.OrderBy(x => x.CreatedAt).ToList();
                SessionCode = sessionCode ?? ResolveSessionCode(Orders);
            }

            public List<OrderEntity> Orders { get; }
            public OrderEntity PrimaryOrder => Orders[0];
            public string? SessionCode { get; }
            public bool IsGroupedBill => Orders.Count > 1;
            public List<Guid> OrderIds => Orders.Select(x => x.Id).ToList();
            public string? TableNumber => PrimaryOrder.Table?.TableNumber;
            public DateTime FirstOrderCreatedAt => Orders.Min(x => x.CreatedAt);
            public DateTime LastOrderCreatedAt => Orders.Max(x => x.CreatedAt);
            public DateTime? LastUpdatedAt => Orders
                .Select(x => x.UpdatedAt ?? x.CreatedAt)
                .OrderByDescending(x => x)
                .FirstOrDefault();
            public OrderStatus Status => CalculateBillStatus(Orders);
            public decimal SubTotal => Orders.Sum(x => x.SubTotal);
            public decimal VatAmount => Orders.Sum(x => x.VatAmount);
            public decimal ServiceChargeAmount => Orders.Sum(x => x.ServiceChargeAmount);
            public decimal DiscountAmount => Orders.Sum(x => x.DiscountAmount);
            public decimal TotalAmount => Orders.Sum(x => x.TotalAmount);
        }

        private static List<CashierBillContext> BuildCashierBillContexts(
            IEnumerable<OrderEntity> orders,
            IEnumerable<QrSession> sessions)
        {
            var sessionList = sessions
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
            var groups = new Dictionary<string, (string? SessionCode, List<OrderEntity> Orders)>();

            foreach (var order in orders.OrderBy(x => x.CreatedAt))
            {
                var session = order.Status == OrderStatus.Cancelled
                    ? null
                    : FindSessionForOrder(order, sessionList);
                var key = session is null ? $"order:{order.Id}" : $"session:{session.Id}";

                if (!groups.TryGetValue(key, out var group))
                {
                    group = (session?.SessionToken, new List<OrderEntity>());
                    groups[key] = group;
                }

                group.Orders.Add(order);
            }

            return groups.Values
                .Where(x => x.Orders.Count > 0)
                .Select(x => new CashierBillContext(x.Orders, x.SessionCode))
                .ToList();
        }

        private static QrSession? FindSessionForOrder(OrderEntity order, IEnumerable<QrSession> sessions)
        {
            if (!order.TableId.HasValue)
            {
                return null;
            }

            return sessions.FirstOrDefault(session =>
                session.BranchId == order.BranchId
                && session.TableId == order.TableId.Value
                && order.CreatedAt >= session.CreatedAt
                && order.CreatedAt <= session.ExpiresAt);
        }

        private static string? ResolveSessionCode(IEnumerable<OrderEntity> orders)
        {
            var now = DateTime.UtcNow;
            return orders
                .SelectMany(x => x.QrSessions)
                .Where(x => x.IsActive && x.ExpiresAt > now)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault()
                ?.SessionToken;
        }

        private static OrderStatus CalculateBillStatus(IEnumerable<OrderEntity> orders)
        {
            var orderList = orders.ToList();
            if (orderList.Count == 0)
            {
                return OrderStatus.Cancelled;
            }

            if (orderList.All(x => x.Status == OrderStatus.Completed))
            {
                return OrderStatus.Completed;
            }

            if (orderList.All(x => x.Status == OrderStatus.Cancelled))
            {
                return OrderStatus.Cancelled;
            }

            return orderList
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .First()
                .Status;
        }

        private static string? BuildGroupedCustomerNote(CashierBillContext bill)
        {
            var notes = bill.Orders
                .Select(x => x.CustomerNote)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return notes.Count == 0 ? null : string.Join(" | ", notes);
        }

        private static List<CustomerOrderItemResponse> BuildGroupedItems(IEnumerable<OrderEntity> orders)
        {
            return orders
                .SelectMany(order => order.Items)
                .OrderBy(item => item.CreatedAt)
                .GroupBy(item => new
                {
                    item.MenuItemId,
                    item.MenuItemName,
                    item.UnitPrice,
                    Note = item.Note?.Trim() ?? string.Empty
                })
                .Select(group =>
                {
                    var first = group.First();
                    return new CustomerOrderItemResponse
                    {
                        OrderItemId = first.Id,
                        MenuItemId = first.MenuItemId,
                        MenuItemName = first.MenuItemName,
                        UnitPrice = first.UnitPrice,
                        Quantity = group.Sum(x => x.Quantity),
                        SubTotal = group.Sum(x => x.SubTotal),
                        Note = string.IsNullOrWhiteSpace(first.Note) ? null : first.Note,
                        Status = CalculateItemStatus(group),
                        EstimatedCookingMinutes = first.EstimatedCookingMinutes
                    };
                })
                .ToList();
        }

        private static OrderItemStatus CalculateItemStatus(IEnumerable<OrderItem> items)
        {
            var itemList = items.ToList();
            if (itemList.All(x => x.Status == OrderItemStatus.Served))
            {
                return OrderItemStatus.Served;
            }

            if (itemList.All(x => x.Status == OrderItemStatus.Cancelled))
            {
                return OrderItemStatus.Cancelled;
            }

            return itemList
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .First()
                .Status;
        }

        private static IEnumerable<Guid> GetCoveredOrderIds(PayOsPaymentSnapshot? snapshot, CashierBillContext bill)
        {
            return snapshot?.CoveredOrderIds?.Length > 0
                ? snapshot.CoveredOrderIds.Distinct()
                : bill.OrderIds;
        }

        private static decimal GetOrderGrossAmount(OrderEntity order)
        {
            return order.SubTotal + order.VatAmount + order.ServiceChargeAmount;
        }

        private static TableOrderHistoryResponse MapOrder(OrderEntity order, string? sessionCode = null)
        {
            var latestPayment = GetLatestPayment(order);
            return new TableOrderHistoryResponse
            {
                OrderId = order.Id,
                PrimaryOrderId = order.Id,
                IsGroupedBill = false,
                OrderIds = new List<Guid> { order.Id },
                OrderNumber = order.OrderNumber,
                BranchId = order.BranchId,
                TableId = order.TableId,
                TableNumber = order.Table?.TableNumber,
                SessionCode = sessionCode ?? order.QrSessions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.SessionToken,
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
