using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Repositories
{
    public class KitchenRepository : IKitchenRepository
    {
        private readonly ApplicationDbContext _context;

        public KitchenRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<OrderItem>> GetActiveKitchenItemsAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.OrderItems
                .Include(x => x.Order)
                    .ThenInclude(o => o.Table)
                .Include(x => x.MenuItem)
                .Where(x =>
                    x.Order.BranchId == branchId &&
                    (x.Status == OrderItemStatus.Confirmed || x.Status == OrderItemStatus.Cooking) &&
                    x.Order.Status != OrderStatus.Cancelled &&
                    x.Order.Status != OrderStatus.Completed)
                .OrderBy(x => x.ConfirmedAt)
                .ToListAsync(ct);
        }

        public Task<List<OrderItem>> GetOrderItemsByIdsAsync(List<Guid> itemIds, CancellationToken ct = default)
        {
            return _context.OrderItems
                .Include(x => x.Order)
                .Where(x => itemIds.Contains(x.Id))
                .ToListAsync(ct);
        }

        public Task<List<Order>> GetOrdersByIdsAsync(List<Guid> orderIds, CancellationToken ct = default)
        {
            return _context.Orders
                .Include(x => x.Items)
                .Where(x => orderIds.Contains(x.Id))
                .ToListAsync(ct);
        }
    }
}
