using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Tests.Infrastructure;

/// <summary>
/// Builds in-memory entity graphs for test scenarios.
/// All IDs are freshly generated so tests don't collide with each other.
/// The created entities are saved via the test's DbContext (inside a rolled-back
/// transaction), so they never actually persist.
/// </summary>
public static class TestDataSeeder
{
    // Real IDs from cau-ca restaurant — used as a stable base (read-only in tests).
    public static readonly Guid RestaurantId = Guid.Parse("7c59e37f-c7e8-4a2f-a350-c9d8fb95ab68");
    public static readonly Guid BranchId     = Guid.Parse("62f90421-46e0-4221-9da0-4dd434dc8031");
    public static readonly Guid TableId      = Guid.Parse("bea9263a-1b80-4791-bd32-8af4ae9aed0e");
    public static readonly Guid MenuItemId   = Guid.Parse("894e8702-8584-4bb9-977e-287eeedc9e83"); // Bun Bo 10000

    /// <summary>Creates a minimal Order that can be saved and then referenced by a QrSession.</summary>
    public static Order BuildOrder(Guid? id = null) => new()
    {
        Id                    = id ?? Guid.NewGuid(),
        BranchId              = BranchId,
        TableId               = TableId,
        OrderNumber           = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
        SubTotal              = 10000m,
        VatPercent            = 0,
        VatAmount             = 0,
        ServiceChargePercent  = 0,
        ServiceChargeAmount   = 0,
        DiscountAmount        = 0,
        TotalAmount           = 10000m,
        Status                = OrderStatus.PendingConfirmation,
        OrderSource           = OrderSource.QR,
        CreatedAt             = DateTime.UtcNow,
        UpdatedAt             = DateTime.UtcNow,
        Items                 = new List<OrderItem>
        {
            new()
            {
                Id                      = Guid.NewGuid(),
                MenuItemId              = MenuItemId,
                MenuItemName            = "Bun Bo",
                UnitPrice               = 10000m,
                Quantity                = 1,
                SubTotal                = 10000m,
                Status                  = OrderItemStatus.Pending,
                EstimatedCookingMinutes = 10,
                CreatedAt               = DateTime.UtcNow,
                UpdatedAt               = DateTime.UtcNow
            }
        }
    };

    /// <summary>Creates a QrSession that already has an ActiveOrderId (the multi-person scenario).</summary>
    public static QrSession BuildActiveSession(Guid orderId, string? sessionToken = null) => new()
    {
        Id              = Guid.NewGuid(),
        BranchId        = BranchId,
        TableId         = TableId,
        SessionToken    = sessionToken ?? GenerateToken(),
        IsActive        = true,
        ActiveOrderId   = orderId,
        ExpiresAt       = DateTime.UtcNow.AddHours(3),
        CreatedAt       = DateTime.UtcNow,
        UpdatedAt       = DateTime.UtcNow
    };

    private static string GenerateToken() =>
        Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
