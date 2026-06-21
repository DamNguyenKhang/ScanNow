using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Repositories
{
    public class WaiterRepository : IWaiterRepository
    {
        private readonly ApplicationDbContext _context;

        public WaiterRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<Order>> GetPendingConfirmationOrdersAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Orders
                .Include(x => x.Table)
                .Include(x => x.Items)
                    .ThenInclude(i => i.MenuItem)
                .Where(x => x.BranchId == branchId && x.Status == OrderStatus.PendingConfirmation)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<Order?> GetOrderWithItemsAsync(Guid orderId, CancellationToken ct = default)
        {
            return _context.Orders
                .Include(x => x.Table)
                .Include(x => x.Items)
                    .ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        }

        public Task<List<OrderItem>> GetReadyToServeItemsAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.OrderItems
                .Include(x => x.Order)
                    .ThenInclude(o => o.Table)
                .Include(x => x.MenuItem)
                .Where(x =>
                    x.Order.BranchId == branchId &&
                    x.Status == OrderItemStatus.Ready &&
                    x.Order.Status != OrderStatus.Cancelled &&
                    x.Order.Status != OrderStatus.Completed)
                .OrderBy(x => x.ReadyAt)
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
