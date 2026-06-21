using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScanNow.Infrastructure;
using ScanNow.Infrastructure.Repositories;
using ScanNow.Tests.Infrastructure;

namespace ScanNow.Tests;

/// <summary>
/// Tests targeted at the EF Core / Npgsql-specific bugs in OrderRepository.
///
/// ROOT CAUSE EXPLANATION
/// ──────────────────────
/// The ApplicationDbContext defines "Level 2" global query filters that navigate
/// two levels through foreign keys:
///
///   OrderItem  →  HasQueryFilter(e => !IsTenantResolved || e.Order.Branch.RestaurantId == CurrentTenantId)
///   Payment    →  HasQueryFilter(e => !IsTenantResolved || e.Order.Branch.RestaurantId == CurrentTenantId)
///
/// In EF Core 10 + Npgsql 10, two specific query shapes fail to translate when
/// these filters are active:
///
/// BUG 1 — .Include() with navigation filters
///   _context.Orders.Include(x => x.Items)
///   → EF Core must join Orders → OrderItems → Orders (again) → Branches → Restaurants
///     to evaluate the OrderItem filter. With Npgsql 10 this throws
///     InvalidOperationException ("The LINQ expression … could not be translated").
///
/// BUG 2 — ExecuteUpdateAsync with navigation filters
///   _context.Payments.Where(x => x.OrderId == id).ExecuteUpdateAsync(...)
///   → EF Core needs a JOIN in the UPDATE statement. Npgsql translates this
///     differently, and the generated SQL fails at runtime with the same error.
///
/// FIX — IgnoreQueryFilters() + explicit BranchId check
///   .IgnoreQueryFilters() removes ALL global filters for the query **and** all
///   its included navigation properties. Security is preserved by replacing the
///   tenant guard with an explicit `x.BranchId == branchId` predicate, where
///   `branchId` is already validated via the QrSession.
///
/// SECONDARY BUG (AsQueryable().IgnoreQueryFilters() inside Include)
///   An early attempt added IgnoreQueryFilters() inside the Include lambda:
///     .Include(x => x.Items.AsQueryable().IgnoreQueryFilters())
///   This is not a supported EF Core pattern. EF Core tracks the loaded items
///   in an unexpected state, causing SaveChangesAsync to generate
///     UPDATE "OrderItems" WHERE "Id" = &lt;new-guid&gt;
///   instead of INSERT, resulting in DbUpdateConcurrencyException
///   ("expected 1 row affected, got 0").
///
///   The correct fix is plain .Include(x => x.Items) after .IgnoreQueryFilters()
///   on the root — the root-level call already suppresses filters for all includes.
/// </summary>
[Collection("DatabaseTests")]
public class OrderRepositoryTests : IAsyncLifetime
{
    private ApplicationDbContext _ctx = null!;
    private IDbContextTransaction _tx = null!;

    public async Task InitializeAsync()
    {
        // Tenant IS resolved — this makes global query filters active,
        // which is exactly the scenario that caused the original bugs.
        _ctx = TestDbContextFactory.Create(
            TestTenantContext.ForRestaurant(TestDataSeeder.RestaurantId, "cau-ca"));

        // Wrap everything in a transaction so no test data ever persists.
        _tx = await _ctx.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _ctx.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // TC-1  GetActiveOrderByIdAsync — BUG 1 regression
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Without IgnoreQueryFilters(), EF Core 10 / Npgsql 10 throws
    /// InvalidOperationException when trying to apply the Level-2 query filter
    /// on OrderItems inside Include. This test verifies the fix works.
    /// </summary>
    [Fact(DisplayName = "TC-1 GetActiveOrderByIdAsync: loads order+items when tenant is resolved (no InvalidOperationException)")]
    public async Task GetActiveOrderByIdAsync_WithResolvedTenant_ReturnsOrderWithItems()
    {
        // Arrange — save an order with one item inside our rollback-safe transaction.
        var order = TestDataSeeder.BuildOrder();
        _ctx.Orders.Add(order);
        await _ctx.SaveChangesAsync();

        // Detach so the repository re-fetches from DB (not from change-tracker cache).
        _ctx.ChangeTracker.Clear();

        // Act — this call previously threw InvalidOperationException.
        var repo = new OrderRepository(_ctx);
        var result = await repo.GetActiveOrderByIdAsync(order.Id, TestDataSeeder.BranchId);

        // Assert
        result.Should().NotBeNull("order exists in DB with the correct BranchId");
        result!.Id.Should().Be(order.Id);
        result.Items.Should().HaveCount(1, "one OrderItem was seeded");
    }

    // ─────────────────────────────────────────────────────────────
    // TC-2  SaveChangesAsync after loading — BUG 2 regression
    //       (DbUpdateConcurrencyException from wrong Include syntax)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// After loading an order with GetActiveOrderByIdAsync, adding a new item
    /// and calling SaveChangesAsync must generate INSERT (not UPDATE) for the
    /// new item.  With the broken Include syntax, EF tracked new items as
    /// Modified → generated UPDATE WHERE Id=&lt;new-guid&gt; → 0 rows → exception.
    /// </summary>
    [Fact(DisplayName = "TC-2 Adding item after GetActiveOrderByIdAsync: SaveChanges inserts new item (no DbUpdateConcurrencyException)")]
    public async Task GetActiveOrderByIdAsync_ThenAddItem_SaveChangesSucceeds()
    {
        // Arrange
        var order = TestDataSeeder.BuildOrder();
        _ctx.Orders.Add(order);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        var repo = new OrderRepository(_ctx);
        var loaded = await repo.GetActiveOrderByIdAsync(order.Id, TestDataSeeder.BranchId);
        loaded.Should().NotBeNull();

        // Simulate what PlaceOrderAsync does: add new item to the loaded order.
        var newItem = new ScanNow.Domain.Entities.OrderItem
        {
            Id                      = Guid.NewGuid(),
            OrderId                 = loaded!.Id,
            MenuItemId              = TestDataSeeder.MenuItemId,
            MenuItemName            = "Bun Bo (Person B)",
            UnitPrice               = 10_000m,
            Quantity                = 2,
            SubTotal                = 20_000m,
            Status                  = ScanNow.Domain.Enums.OrderItemStatus.Pending,
            EstimatedCookingMinutes = 10,
            CreatedAt               = DateTime.UtcNow,
            UpdatedAt               = DateTime.UtcNow
        };
        loaded.Items.Add(newItem); // in-memory collection update
        loaded.TotalAmount += 20_000m;
        loaded.UpdatedAt = DateTime.UtcNow;

        // THE FIX: ICollection.Add() does NOT auto-track new entities when the parent
        // Order was freshly loaded into a scoped DbContext (real production scenario).
        // AddOrderItemsAsync explicitly registers the items as Added.
        await repo.AddOrderItemsAsync([newItem]);

        // Act
        var act = async () => await _ctx.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "AddOrderItemsAsync explicitly registers new items as Added so SaveChanges generates INSERT");

        // Verify the new item was actually INSERTed.
        _ctx.ChangeTracker.Clear();
        var itemInDb = await _ctx.OrderItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == newItem.Id);
        itemInDb.Should().NotBeNull("the new OrderItem must be persisted");
        itemInDb!.MenuItemName.Should().Be("Bun Bo (Person B)");
    }

    // ─────────────────────────────────────────────────────────────
    // TC-3  MarkPendingPaymentsFailedAsync — ExecuteUpdateAsync bug
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ExecuteUpdateAsync on Payments previously failed because EF Core tried to
    /// JOIN through Order → Branch → Restaurant to evaluate the Level-2 filter.
    /// With IgnoreQueryFilters() the UPDATE is a plain WHERE orderId = X.
    /// </summary>
    [Fact(DisplayName = "TC-3 MarkPendingPaymentsFailedAsync: completes without SQL translation error")]
    public async Task MarkPendingPaymentsFailedAsync_DoesNotThrow()
    {
        // Arrange — order + a PENDING payment
        var order = TestDataSeeder.BuildOrder();
        _ctx.Orders.Add(order);
        await _ctx.SaveChangesAsync();

        var payment = new ScanNow.Domain.Entities.Payment
        {
            Id        = Guid.NewGuid(),
            OrderId   = order.Id,
            Method    = ScanNow.Domain.Enums.PaymentMethod.CASH,
            Status    = ScanNow.Domain.Enums.PaymentStatus.PENDING,
            Amount    = order.TotalAmount,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Payments.Add(payment);
        await _ctx.SaveChangesAsync();

        // Act
        var repo = new OrderRepository(_ctx);
        var act  = async () => await repo.MarkPendingPaymentsFailedAsync(order.Id, DateTime.UtcNow);

        // Assert
        await act.Should().NotThrowAsync();

        // Verify the status was actually updated.
        _ctx.ChangeTracker.Clear();
        var updated = await _ctx.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == payment.Id);
        updated!.Status.Should().Be(ScanNow.Domain.Enums.PaymentStatus.FAILED);
    }
}
