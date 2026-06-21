using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Infrastructure;
using ScanNow.Infrastructure.Repositories;
using ScanNow.Tests.Infrastructure;

namespace ScanNow.Tests;

/// <summary>
/// Diagnostic tests — run with SQL logging to understand exactly what EF Core generates.
/// These tests expose the root cause step by step.
/// </summary>
[Collection("DatabaseTests")]
public class DiagnosticTests : IAsyncLifetime
{
    private ApplicationDbContext _ctx = null!;
    private IDbContextTransaction _tx  = null!;

    public async Task InitializeAsync()
    {
        // Enable SQL logging to see exactly what EF generates
        _ctx = TestDbContextFactory.Create(
            TestTenantContext.ForRestaurant(TestDataSeeder.RestaurantId, "cau-ca"),
            logSql: true);
        _tx = await _ctx.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _ctx.DisposeAsync();
    }

    /// <summary>
    /// Diagnosis: after loading with IgnoreQueryFilters().Include(x => x.Items),
    /// what EntityState does EF assign to the existing item and the new item?
    /// </summary>
    [Fact(DisplayName = "DIAG-1 Check entity states after IgnoreQueryFilters load + new item add")]
    public async Task DiagnoseEntityStatesAfterIgnoreQueryFiltersLoad()
    {
        // Arrange — save order with 1 item
        var order = TestDataSeeder.BuildOrder();
        _ctx.Orders.Add(order);
        await _ctx.SaveChangesAsync();
        var existingItemId = order.Items.First().Id;

        _ctx.ChangeTracker.Clear();

        // Load with IgnoreQueryFilters
        var repo = new OrderRepository(_ctx);
        var loaded = await repo.GetActiveOrderByIdAsync(order.Id, TestDataSeeder.BranchId);
        loaded.Should().NotBeNull();

        // Check state of loaded items
        var existingItemEntry = _ctx.Entry(loaded!.Items.First());
        Console.WriteLine($"[DIAG] Existing item state after IgnoreQueryFilters load: {existingItemEntry.State}");

        // Add new item
        var newItem = new OrderItem
        {
            Id                      = Guid.NewGuid(),
            MenuItemId              = TestDataSeeder.MenuItemId,
            MenuItemName            = "New Item",
            UnitPrice               = 10_000m,
            Quantity                = 1,
            SubTotal                = 10_000m,
            Status                  = OrderItemStatus.Pending,
            EstimatedCookingMinutes = 10,
            CreatedAt               = DateTime.UtcNow,
            UpdatedAt               = DateTime.UtcNow
        };
        loaded.Items.Add(newItem);

        var newItemState = _ctx.Entry(newItem).State;
        var existingItemStateAfterAdd = _ctx.Entry(loaded.Items.First(x => x.Id == existingItemId)).State;
        var orderState = _ctx.Entry(loaded).State;

        Console.WriteLine($"[DIAG] New item state after Add: {newItemState}");
        Console.WriteLine($"[DIAG] Existing item state after Add: {existingItemStateAfterAdd}");
        Console.WriteLine($"[DIAG] Order state: {orderState}");

        // These assertions document expected vs actual
        newItemState.Should().Be(EntityState.Added, "new item should be Added");
        existingItemStateAfterAdd.Should().Be(EntityState.Unchanged, "existing item should stay Unchanged");
    }

    /// <summary>
    /// Diagnosis: does the same issue occur WITHOUT IgnoreQueryFilters (using unresolved tenant)?
    /// This isolates whether IgnoreQueryFilters is the cause.
    /// </summary>
    [Fact(DisplayName = "DIAG-2 Same test WITHOUT IgnoreQueryFilters (unresolved tenant) — compare behavior")]
    public async Task DiagnoseWithoutIgnoreQueryFilters()
    {
        // Use a SEPARATE context with unresolved tenant (no filters applied)
        await using var ctx2 = TestDbContextFactory.Create(
            TestTenantContext.Unresolved(),
            logSql: true);
        // Share the same transaction if possible — just use a separate connection
        // Actually can't share tx across connections easily, so just run standalone
        // (no rollback for this ctx; data will remain but we'll identify by DIAG prefix)

        // Arrange
        var order = TestDataSeeder.BuildOrder();
        order.OrderNumber = "DIAG2-" + order.OrderNumber; // label test data
        ctx2.Orders.Add(order);
        await ctx2.SaveChangesAsync();

        ctx2.ChangeTracker.Clear();

        // Load WITHOUT IgnoreQueryFilters (unresolved tenant = no filter)
        var loaded = await ctx2.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == order.Id
                && x.BranchId == TestDataSeeder.BranchId
                && x.Status != OrderStatus.Cancelled
                && x.Status != OrderStatus.Completed);

        loaded.Should().NotBeNull();

        var existingItemId = loaded!.Items.First().Id;
        Console.WriteLine($"[DIAG2] Existing item state after plain Include load: {ctx2.Entry(loaded.Items.First()).State}");

        // Add new item
        var newItem = new OrderItem
        {
            Id                      = Guid.NewGuid(),
            MenuItemId              = TestDataSeeder.MenuItemId,
            MenuItemName            = "DIAG2 New Item",
            UnitPrice               = 10_000m,
            Quantity                = 1,
            SubTotal                = 10_000m,
            Status                  = OrderItemStatus.Pending,
            EstimatedCookingMinutes = 10,
            CreatedAt               = DateTime.UtcNow,
            UpdatedAt               = DateTime.UtcNow
        };
        loaded.Items.Add(newItem);

        Console.WriteLine($"[DIAG2] New item state: {ctx2.Entry(newItem).State}");
        Console.WriteLine($"[DIAG2] Existing item state after Add: {ctx2.Entry(loaded.Items.First(x => x.Id == existingItemId)).State}");

        var act = async () => await ctx2.SaveChangesAsync();
        await act.Should().NotThrowAsync();

        // Cleanup DIAG2 data (since it's outside our main transaction)
        ctx2.Orders.Remove(loaded);
        await ctx2.SaveChangesAsync();
    }
}
