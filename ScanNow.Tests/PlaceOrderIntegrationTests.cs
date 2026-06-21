using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScanNow.Application.Features.Order;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.Order.Validators;
using ScanNow.Infrastructure;
using ScanNow.Infrastructure.Repositories;
using ScanNow.Tests.Infrastructure;

namespace ScanNow.Tests;

/// <summary>
/// Integration tests for PlaceOrderAsync — verifies that each call always creates
/// a new, independent order associated with the active session.
///
/// Scenario summary:
///   1. Staff opens table → QrSession is created with no ActiveOrderId.
///   2. Person A places order → a new Order is created, session.ActiveOrderId is set to order-A.
///   3. Person B (or Person A ordering again) places another order in the same session →
///      a SECOND, fully independent Order is created, and session.ActiveOrderId is updated
///      to point to the latest order. Both orders remain queryable via the session.
/// </summary>
[Collection("DatabaseTests")]
public class PlaceOrderIntegrationTests : IAsyncLifetime
{
    private ApplicationDbContext _ctx  = null!;
    private IDbContextTransaction _tx  = null!;
    private OrderService _service      = null!;

    public async Task InitializeAsync()
    {
        _ctx = TestDbContextFactory.Create(
            TestTenantContext.ForRestaurant(TestDataSeeder.RestaurantId, "cau-ca"));

        _tx = await _ctx.Database.BeginTransactionAsync();

        var repo       = new OrderRepository(_ctx);
        var uow        = new UnitOfWork(_ctx);
        IValidator<PlaceOrderRequest> validator = new PlaceOrderRequestValidator();
        var publisher  = new NoOpOrderPublisher();
        var cartService = new NoOpCartService();

        _service = new OrderService(repo, uow, validator, publisher, cartService);
    }

    public async Task DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _ctx.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // TC-4  The main 500 scenario: two people, one session
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Person A places an order → session.ActiveOrderId is set.
    /// Person B (or same person ordering again) places another order in the same session → must create a NEW, separate order.
    /// Each call to PlaceOrderAsync always creates an independent order.
    /// </summary>
    [Fact(DisplayName = "TC-4 PlaceOrderAsync: second order in same session creates a separate new order")]
    public async Task PlaceOrderAsync_SecondOrderInSameSession_CreatesNewSeparateOrder()
    {
        // ── Step 1: Person A places first order ──────────────────
        var sessionToken = "TST" + Guid.NewGuid().ToString("N")[..3].ToUpper();

        // Seed a QrSession with NO active order (fresh session).
        var session = TestDataSeeder.BuildActiveSession(orderId: Guid.Empty);

        // Manually clear ActiveOrderId to simulate "session just opened".
        session.ActiveOrderId = null;
        session.SessionToken  = sessionToken;
        _ctx.QrSessions.Add(session);
        await _ctx.SaveChangesAsync();

        var requestA = new PlaceOrderRequest
        {
            CustomerName = "Person A",
            Items        = [new OrderItemRequest { MenuItemId = TestDataSeeder.MenuItemId, Quantity = 1 }]
        };

        var responseA = await _service.PlaceOrderAsync(sessionToken, requestA);

        responseA.Should().NotBeNull();
        responseA.OrderId.Should().NotBeEmpty("Person A's order must be created");
        responseA.Items.Should().HaveCount(1, "Person A ordered 1 item");

        // Verify the session now has an ActiveOrderId pointing to order A.
        _ctx.ChangeTracker.Clear();
        var sessionAfterA = await _ctx.QrSessions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.SessionToken == sessionToken);
        sessionAfterA.ActiveOrderId.Should().Be(responseA.OrderId,
            "after first order, session.ActiveOrderId must point to Person A's order");

        // ── Step 2: Person B places order in same session ─────────
        var requestB = new PlaceOrderRequest
        {
            CustomerName = "Person B",
            Items        = [new OrderItemRequest { MenuItemId = TestDataSeeder.MenuItemId, Quantity = 2 }]
        };

        var responseB = await _service.PlaceOrderAsync(sessionToken, requestB);

        // ── Assertions ────────────────────────────────────────────
        responseB.Should().NotBeNull();
        responseB.OrderId.Should().NotBeEmpty("Person B's order must be created");
        responseB.OrderId.Should().NotBe(responseA.OrderId,
            "each PlaceOrderAsync call must create a NEW separate order, not merge into the existing one");
        responseB.Items.Should().HaveCount(1, "Person B's order has only Person B's 2-qty item");
        responseB.Items[0].Quantity.Should().Be(2, "Person B ordered quantity 2");

        // Session.ActiveOrderId should now point to the latest order (B's).
        _ctx.ChangeTracker.Clear();
        var sessionAfterB = await _ctx.QrSessions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.SessionToken == sessionToken);
        sessionAfterB.ActiveOrderId.Should().Be(responseB.OrderId,
            "after second order, session.ActiveOrderId must be updated to point to Person B's order");
    }

    // ─────────────────────────────────────────────────────────────
    // TC-5  Happy path: first order in a fresh session
    // ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TC-5 PlaceOrderAsync: first order in fresh session creates order and sets ActiveOrderId")]
    public async Task PlaceOrderAsync_FirstOrder_CreatesOrderAndUpdatesSession()
    {
        var sessionToken = "FRS" + Guid.NewGuid().ToString("N")[..3].ToUpper();
        var session      = TestDataSeeder.BuildActiveSession(Guid.Empty);
        session.ActiveOrderId = null;
        session.SessionToken  = sessionToken;
        _ctx.QrSessions.Add(session);
        await _ctx.SaveChangesAsync();

        var request = new PlaceOrderRequest
        {
            Items = [new OrderItemRequest { MenuItemId = TestDataSeeder.MenuItemId, Quantity = 3 }]
        };

        var response = await _service.PlaceOrderAsync(sessionToken, request);

        response.OrderId.Should().NotBeEmpty();
        response.Items.Should().HaveCount(1);
        response.Items[0].Quantity.Should().Be(3);

        _ctx.ChangeTracker.Clear();
        var updatedSession = await _ctx.QrSessions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.SessionToken == sessionToken);
        updatedSession.ActiveOrderId.Should().Be(response.OrderId);
    }

    // ─────────────────────────────────────────────────────────────
    // TC-6  Validation: empty items list must return 400 (not 500)
    // ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TC-6 PlaceOrderAsync: empty items list throws ValidationException (not 500)")]
    public async Task PlaceOrderAsync_EmptyItems_ThrowsValidationException()
    {
        var sessionToken = "VAL" + Guid.NewGuid().ToString("N")[..3].ToUpper();
        var session      = TestDataSeeder.BuildActiveSession(Guid.Empty);
        session.ActiveOrderId = null;
        session.SessionToken  = sessionToken;
        _ctx.QrSessions.Add(session);
        await _ctx.SaveChangesAsync();

        var request = new PlaceOrderRequest { Items = [] };

        var act = async () => await _service.PlaceOrderAsync(sessionToken, request);

        await act.Should().ThrowAsync<ValidationException>(
            "FluentValidation must reject an order with no items before any DB access");
    }

    // ─────────────────────────────────────────────────────────────
    // TC-7  Expired / non-existent session must return 404
    // ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TC-7 PlaceOrderAsync: non-existent session code throws NotFoundException")]
    public async Task PlaceOrderAsync_InvalidSessionCode_ThrowsNotFoundException()
    {
        var request = new PlaceOrderRequest
        {
            Items = [new OrderItemRequest { MenuItemId = TestDataSeeder.MenuItemId, Quantity = 1 }]
        };

        var act = async () => await _service.PlaceOrderAsync("XXXXXX", request);

        await act.Should().ThrowAsync<ScanNow.Domain.Exceptions.NotFoundException>(
            "an unknown session code must yield a 404, not a 500");
    }
}
