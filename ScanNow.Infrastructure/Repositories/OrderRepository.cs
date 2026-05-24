using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<QrSession?> GetActiveSessionByCodeAsync(string sessionCode, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.QrSessions
                .Include(x => x.Table)
                .Include(x => x.Branch)
                .FirstOrDefaultAsync(
                    x => x.SessionToken == sessionCode && x.IsActive && x.ExpiresAt > now,
                    ct);
        }

        public Task<MenuItem?> GetMenuItemByIdAsync(Guid menuItemId, CancellationToken ct = default)
        {
            return _context.MenuItems
                .FirstOrDefaultAsync(x => x.Id == menuItemId, ct);
        }

        public Task<Order?> GetActiveOrderByIdAsync(Guid orderId, CancellationToken ct = default)
        {
            return _context.Orders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x => x.Id == orderId
                         && x.Status != OrderStatus.Cancelled
                         && x.Status != OrderStatus.Completed,
                    ct);
        }

        public Task<Order?> GetActiveSessionOrderAsync(string sessionCode, Guid orderId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.Orders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x => x.Id == orderId
                         && x.QrSessions.Any(session =>
                             session.SessionToken == sessionCode
                             && session.ActiveOrderId == orderId
                             && session.IsActive
                             && session.ExpiresAt > now),
                    ct);
        }

        public Task<Order?> GetOrderWithPaymentsAsync(Guid orderId, CancellationToken ct = default)
        {
            return _context.Orders
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        }

        public Task AddOrderAsync(Order order, CancellationToken ct = default)
        {
            return _context.Orders.AddAsync(order, ct).AsTask();
        }
    }
}
