using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Cashier;
using ScanNow.Application.Features.Cashier.DTOs;
using ScanNow.Application.Features.Cashier.Validators;
using ScanNow.Application.Features.Common;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Infrastructure;
using ScanNow.Infrastructure.Repositories;
using ScanNow.Tests.Infrastructure;

namespace ScanNow.Tests;

[Collection("DatabaseTests")]
public class CashierBillIntegrationTests : IAsyncLifetime
{
    private ApplicationDbContext _ctx = null!;
    private IDbContextTransaction _tx = null!;
    private CashierService _service = null!;
    private FakePaymentService _paymentService = null!;
    private Guid _ownerId;

    public async Task InitializeAsync()
    {
        _ctx = TestDbContextFactory.Create(
            TestTenantContext.ForRestaurant(TestDataSeeder.RestaurantId, "cau-ca"));
        _tx = await _ctx.Database.BeginTransactionAsync();

        _ownerId = await _ctx.Branches
            .IgnoreQueryFilters()
            .Where(x => x.Id == TestDataSeeder.BranchId)
            .Select(x => x.Restaurant.OwnerId)
            .FirstAsync();

        _paymentService = new FakePaymentService();
        _service = BuildService(_ownerId);
    }

    public async Task DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _ctx.DisposeAsync();
    }

    [Fact(DisplayName = "Cashier bill preview groups all active orders in the same QR session")]
    public async Task GetBillAsync_ActiveSession_ReturnsGroupedBill()
    {
        var (firstOrder, secondOrder, sessionCode, _) = await SeedTwoActiveOrdersAsync();

        var bill = await _service.GetBillAsync(TestDataSeeder.BranchId, secondOrder.Id);

        bill.IsGroupedBill.Should().BeTrue();
        bill.PrimaryOrderId.Should().Be(firstOrder.Id);
        bill.SessionCode.Should().Be(sessionCode);
        bill.OrderIds.Should().Equal(firstOrder.Id, secondOrder.Id);
        bill.TotalAmount.Should().Be(20_000m);
        bill.Orders.Should().HaveCount(2);
        bill.Orders.Should().OnlyContain(x => x.SessionCode == sessionCode);
    }

    [Fact(DisplayName = "Cashier history returns one grouped invoice for orders in the same session")]
    public async Task GetBranchOrdersAsync_PaidHistory_GroupsOrdersInSameSession()
    {
        var (firstOrder, secondOrder, sessionCode, _) = await SeedTwoActiveOrdersAsync();
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = firstOrder.Id,
            Amount = 20_000m,
            Method = PaymentMethod.PAYOS,
            Status = PaymentStatus.SUCCESS,
            PaidAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _ctx.Payments.Add(payment);
        await _ctx.Orders
            .IgnoreQueryFilters()
            .Where(x => x.Id == firstOrder.Id || x.Id == secondOrder.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, OrderStatus.Completed)
                .SetProperty(x => x.CompletedAt, now)
                .SetProperty(x => x.UpdatedAt, now));
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        var result = await _service.GetBranchOrdersAsync(
            TestDataSeeder.BranchId,
            new CashierOrderQuery
            {
                Status = "paid",
                Search = sessionCode,
                PageSize = 10
            });

        result.Items.Should().ContainSingle();
        var invoice = result.Items.Single();
        invoice.IsGroupedBill.Should().BeTrue();
        invoice.OrderNumber.Should().BeEmpty();
        invoice.OrderIds.Should().BeEquivalentTo([firstOrder.Id, secondOrder.Id]);
        invoice.SessionCode.Should().Be(sessionCode);
        invoice.TotalAmount.Should().Be(20_000m);
        invoice.PaymentStatus.Should().Be(PaymentStatus.SUCCESS.ToString());
        invoice.Items.Should().ContainSingle();
        invoice.Items.Single().Quantity.Should().Be(2);
        invoice.Orders.Should().HaveCount(2);
        invoice.Orders.Select(x => x.OrderId).Should().BeEquivalentTo([firstOrder.Id, secondOrder.Id]);
        invoice.Orders.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.OrderNumber));
        invoice.Orders.Should().OnlyContain(x => x.Items.Count == 1);

        var detail = await _service.GetBranchOrderAsync(TestDataSeeder.BranchId, firstOrder.Id);
        detail.IsGroupedBill.Should().BeTrue();
        detail.OrderNumber.Should().Be(firstOrder.OrderNumber);
        detail.OrderIds.Should().BeEquivalentTo([firstOrder.Id, secondOrder.Id]);
        detail.TotalAmount.Should().Be(20_000m);
        detail.Items.Single().Quantity.Should().Be(2);
        detail.Orders.Should().HaveCount(2);
        detail.Orders.Select(x => x.OrderId).Should().BeEquivalentTo([firstOrder.Id, secondOrder.Id]);
    }

    [Fact(DisplayName = "Cashier cash checkout from any order completes the whole session bill")]
    public async Task CheckoutAsync_Cash_CompletesAllOrdersInBill()
    {
        var (firstOrder, secondOrder, _, _) = await SeedTwoActiveOrdersAsync();

        var response = await _service.CheckoutAsync(
            TestDataSeeder.BranchId,
            secondOrder.Id,
            new CashierCheckoutRequest
            {
                PaymentMethod = PaymentMethod.CASH,
                AmountReceived = 50_000m
            });

        response.PaymentStatus.Should().Be(PaymentStatus.SUCCESS);
        response.Bill.OrderIds.Should().Equal(firstOrder.Id, secondOrder.Id);
        response.Bill.TotalAmount.Should().Be(20_000m);
        response.ChangeAmount.Should().Be(30_000m);

        _ctx.ChangeTracker.Clear();
        var orders = await _ctx.Orders
            .IgnoreQueryFilters()
            .Where(x => response.Bill.OrderIds.Contains(x.Id))
            .ToListAsync();
        orders.Should().OnlyContain(x => x.Status == OrderStatus.Completed);
        orders.Should().OnlyContain(x => x.CompletedAt.HasValue);

        var payments = await _ctx.Payments
            .IgnoreQueryFilters()
            .Where(x => response.Bill.OrderIds.Contains(x.OrderId))
            .ToListAsync();
        payments.Should().ContainSingle();
        payments.Single().OrderId.Should().Be(firstOrder.Id);
        payments.Single().Amount.Should().Be(20_000m);
    }

    [Fact(DisplayName = "Cashier voucher checkout applies one discount across the grouped bill")]
    public async Task CheckoutAsync_GroupVoucher_DiscountsBillOnce()
    {
        var (firstOrder, secondOrder, _, _) = await SeedTwoActiveOrdersAsync();
        var voucherCode = "VB" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var voucher = new PaperVoucher
        {
            Id = Guid.NewGuid(),
            BranchId = TestDataSeeder.BranchId,
            Code = voucherCode,
            Name = "Grouped bill test voucher",
            DiscountType = DiscountType.FIXED_AMOUNT,
            DiscountValue = 5_000m,
            MinOrderAmount = 15_000m,
            Quantity = 5,
            UsedCount = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.PaperVouchers.Add(voucher);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        var response = await _service.CheckoutAsync(
            TestDataSeeder.BranchId,
            secondOrder.Id,
            new CashierCheckoutRequest
            {
                PaymentMethod = PaymentMethod.CASH,
                VoucherCode = voucherCode,
                AmountReceived = 20_000m
            });

        response.Bill.DiscountAmount.Should().Be(5_000m);
        response.Bill.TotalAmount.Should().Be(15_000m);
        response.ChangeAmount.Should().Be(5_000m);

        _ctx.ChangeTracker.Clear();
        var orders = await _ctx.Orders
            .IgnoreQueryFilters()
            .Where(x => response.Bill.OrderIds.Contains(x.Id))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        orders[0].Id.Should().Be(firstOrder.Id);
        orders[0].DiscountAmount.Should().Be(5_000m);
        orders[0].TotalAmount.Should().Be(5_000m);
        orders[1].Id.Should().Be(secondOrder.Id);
        orders[1].DiscountAmount.Should().Be(0m);
        orders[1].TotalAmount.Should().Be(10_000m);

        var savedVoucher = await _ctx.PaperVouchers
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == voucher.Id);
        savedVoucher.UsedCount.Should().Be(1);
    }

    [Fact(DisplayName = "Cashier payment cancel fails pending payments across the grouped bill")]
    public async Task CancelBillPendingPaymentAsync_FailsPendingPaymentsAcrossBill()
    {
        var (firstOrder, secondOrder, _, _) = await SeedTwoActiveOrdersAsync();
        var pendingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = firstOrder.Id,
            Amount = 20_000m,
            Method = PaymentMethod.PAYOS,
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Payments.Add(pendingPayment);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        var bill = await _service.CancelBillPendingPaymentAsync(TestDataSeeder.BranchId, secondOrder.Id);

        bill.PaymentStatus.Should().Be(PaymentStatus.FAILED);

        _ctx.ChangeTracker.Clear();
        var savedPayment = await _ctx.Payments
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == pendingPayment.Id);
        savedPayment.Status.Should().Be(PaymentStatus.FAILED);
    }

    [Fact(DisplayName = "Cashier PayOS status completes only the QR snapshot covered orders")]
    public async Task GetBillPaymentStatusAsync_PaidPayOs_CompletesOnlyCoveredOrders()
    {
        var (firstOrder, secondOrder, sessionCode, tableId) = await SeedTwoActiveOrdersAsync();
        await EnablePayOsAsync();

        var payOsResponse = await _service.CheckoutAsync(
            TestDataSeeder.BranchId,
            secondOrder.Id,
            new CashierCheckoutRequest { PaymentMethod = PaymentMethod.PAYOS });

        payOsResponse.PaymentStatus.Should().Be(PaymentStatus.PENDING);
        payOsResponse.Bill.OrderIds.Should().Equal(firstOrder.Id, secondOrder.Id);
        _paymentService.LastCreateInput!.Amount.Should().Be(20_000);

        var thirdOrder = BuildCashierOrder(3, DateTime.UtcNow.AddMinutes(1), tableId);
        _ctx.Orders.Add(thirdOrder);
        var session = await _ctx.QrSessions
            .IgnoreQueryFilters()
            .FirstAsync(x => x.SessionToken == sessionCode);
        session.ActiveOrderId = thirdOrder.Id;
        session.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        _paymentService.StatusResult = PaymentStatusResult.Paid("TX-CASHIER-BILL");
        var status = await _service.GetBillPaymentStatusAsync(TestDataSeeder.BranchId, secondOrder.Id);

        status.PaymentStatus.Should().Be(PaymentStatus.SUCCESS.ToString());

        _ctx.ChangeTracker.Clear();
        var savedOrders = await _ctx.Orders
            .IgnoreQueryFilters()
            .Where(x => x.Id == firstOrder.Id || x.Id == secondOrder.Id || x.Id == thirdOrder.Id)
            .ToListAsync();
        savedOrders.Single(x => x.Id == firstOrder.Id).Status.Should().Be(OrderStatus.Completed);
        savedOrders.Single(x => x.Id == secondOrder.Id).Status.Should().Be(OrderStatus.Completed);
        savedOrders.Single(x => x.Id == thirdOrder.Id).Status.Should().NotBe(OrderStatus.Completed);
    }

    private CashierService BuildService(Guid ownerId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://client.test"
            })
            .Build();

        return new CashierService(
            new OrderRepository(_ctx),
            new TableQrRepository(_ctx),
            new TestCurrentUserService(ownerId, UserRole.OWNER.ToString()),
            new UnitOfWork(_ctx),
            _paymentService,
            new BranchSettingsRepository(_ctx),
            new NoOpOrderPublisher(),
            new CashierOrderQueryValidator(),
            new CashierCheckoutRequestValidator(),
            configuration,
            new TenantUrlBuilder(configuration));
    }

    private async Task<(Order FirstOrder, Order SecondOrder, string SessionCode, Guid TableId)> SeedTwoActiveOrdersAsync()
    {
        var sessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var tableId = Guid.NewGuid();
        var sessionStartedAt = DateTime.UtcNow.AddMinutes(-30);
        var table = new RestaurantTable
        {
            Id = tableId,
            BranchId = TestDataSeeder.BranchId,
            TableNumber = "TEST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            QrCodeToken = Guid.NewGuid().ToString("N"),
            Status = TableStatus.OCCUPIED,
            IsActive = true,
            CreatedAt = sessionStartedAt,
            UpdatedAt = sessionStartedAt
        };
        _ctx.Tables.Add(table);
        await _ctx.SaveChangesAsync();

        var firstOrder = BuildCashierOrder(1, sessionStartedAt.AddMinutes(1), tableId);
        var secondOrder = BuildCashierOrder(2, sessionStartedAt.AddMinutes(2), tableId);

        _ctx.Orders.AddRange(firstOrder, secondOrder);
        await _ctx.SaveChangesAsync();

        var session = TestDataSeeder.BuildActiveSession(secondOrder.Id, sessionCode);
        session.CreatedAt = sessionStartedAt;
        session.UpdatedAt = sessionStartedAt;
        session.ExpiresAt = sessionStartedAt.AddHours(6);
        session.ActiveOrderId = secondOrder.Id;
        session.TableId = tableId;
        _ctx.QrSessions.Add(session);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        return (firstOrder, secondOrder, sessionCode, tableId);
    }

    private static Order BuildCashierOrder(int sequence, DateTime createdAt, Guid tableId)
    {
        var order = TestDataSeeder.BuildOrder();
        order.TableId = tableId;
        order.OrderNumber = $"CASHIER-{sequence}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        order.CustomerName = $"Cashier Bill Customer {sequence}";
        order.SubTotal = 10_000m;
        order.VatPercent = 0m;
        order.VatAmount = 0m;
        order.ServiceChargePercent = 0m;
        order.ServiceChargeAmount = 0m;
        order.DiscountAmount = 0m;
        order.TotalAmount = 10_000m;
        order.Status = OrderStatus.Served;
        order.CreatedAt = createdAt;
        order.UpdatedAt = createdAt;

        foreach (var item in order.Items)
        {
            item.OrderId = order.Id;
            item.UnitPrice = 10_000m;
            item.Quantity = 1;
            item.SubTotal = 10_000m;
            item.Status = OrderItemStatus.Served;
            item.CreatedAt = createdAt;
            item.UpdatedAt = createdAt;
        }

        return order;
    }

    private async Task EnablePayOsAsync()
    {
        var config = await _ctx.BranchPaymentConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.BranchId == TestDataSeeder.BranchId);

        if (config is null)
        {
            config = new BranchPaymentConfig
            {
                Id = Guid.NewGuid(),
                BranchId = TestDataSeeder.BranchId,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.BranchPaymentConfigs.Add(config);
        }

        config.CashEnabled = true;
        config.PayOsEnabled = true;
        config.PayOsClientId = "client-id";
        config.PayOsApiKey = "api-key";
        config.PayOsChecksumKey = "checksum-key";
        config.DefaultMethod = PaymentMethod.PAYOS;
        config.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId, string role)
        {
            UserId = userId;
            Role = role;
        }

        public Guid? UserId { get; }
        public string? Email => "owner@test.local";
        public string? Role { get; }
        public bool IsAuthenticated => true;
    }

    private sealed class FakePaymentService : IPaymentService
    {
        public CreatePaymentLinkInput? LastCreateInput { get; private set; }
        public PaymentStatusResult StatusResult { get; set; } = PaymentStatusResult.NotPaid("PENDING");

        public Task<PaymentLinkResult> CreatePaymentLinkAsync(CreatePaymentLinkInput input)
        {
            LastCreateInput = input;
            return Task.FromResult(new PaymentLinkResult
            {
                Success = true,
                CheckoutUrl = $"https://pay.test/{input.OrderCode}",
                QrCode = "qr-code",
                Bin = "970422",
                AccountNumber = "123456789",
                AccountName = "ScanNow Test",
                Amount = input.Amount,
                Description = input.Description
            });
        }

        public Task<PaymentStatusResult> GetPaymentStatusAsync(long orderCode, PayOSCredentialInput? credentials = null)
        {
            return Task.FromResult(StatusResult);
        }
    }
}
