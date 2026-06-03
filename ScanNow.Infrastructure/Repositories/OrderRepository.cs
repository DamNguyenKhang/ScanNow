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

        public Task<Order?> GetOrderWithDetailsAsync(Guid orderId, CancellationToken ct = default)
        {
            return _context.Orders
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Include(x => x.QrSessions)
                .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        }

        public Task<List<Order>> GetOrdersByBranchAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Include(x => x.QrSessions)
                .Where(x => x.BranchId == branchId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<List<Order>> GetOrdersByBranchTableAsync(Guid branchId, Guid tableId, CancellationToken ct = default)
        {
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Include(x => x.QrSessions)
                .Where(x => x.BranchId == branchId && x.TableId == tableId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<List<Order>> GetActiveSessionOrdersByBranchTableAsync(Guid branchId, Guid tableId, CancellationToken ct = default)
        {
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Include(x => x.QrSessions)
                .Where(x => x.BranchId == branchId
                            && x.TableId == tableId
                            && x.QrSessions.Any(session => session.IsActive && session.TableId == tableId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task AddOrderAsync(Order order, CancellationToken ct = default)
        {
            return _context.Orders.AddAsync(order, ct).AsTask();
        }

        public Task AddPaymentAsync(Payment payment, CancellationToken ct = default)
        {
            return _context.Payments.AddAsync(payment, ct).AsTask();
        }

        public Task<int> MarkPaymentSucceededAsync(Guid paymentId, string? transactionId, DateTime paidAt, CancellationToken ct = default)
        {
            return _context.Payments
                .Where(x => x.Id == paymentId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, PaymentStatus.SUCCESS)
                    .SetProperty(x => x.TransactionId, transactionId)
                    .SetProperty(x => x.PaidAt, paidAt)
                    .SetProperty(x => x.UpdatedAt, paidAt), ct);
        }

        public Task<int> MarkOrderCompletedAsync(Guid orderId, DateTime completedAt, CancellationToken ct = default)
        {
            return _context.Orders
                .Where(x => x.Id == orderId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, OrderStatus.Completed)
                    .SetProperty(x => x.CompletedAt, completedAt)
                    .SetProperty(x => x.UpdatedAt, completedAt), ct);
        }

        public Task<int> TouchOrderAsync(Guid orderId, DateTime updatedAt, CancellationToken ct = default)
        {
            return _context.Orders
                .Where(x => x.Id == orderId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.UpdatedAt, updatedAt), ct);
        }

        public Task<int> MarkPendingPaymentsFailedAsync(Guid orderId, DateTime updatedAt, CancellationToken ct = default)
        {
            return _context.Payments
                .Where(x => x.OrderId == orderId && x.Status == PaymentStatus.PENDING)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, PaymentStatus.FAILED)
                    .SetProperty(x => x.UpdatedAt, updatedAt), ct);
        }
    }
}
