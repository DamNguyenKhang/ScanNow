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

        public Task<Order?> GetActiveOrderByIdAsync(Guid orderId, Guid branchId, CancellationToken ct = default)
        {
            // IgnoreQueryFilters() on the root query also suppresses filters on all includes,
            // so a plain Include(x => x.Items) is sufficient — no need for AsQueryable().IgnoreQueryFilters()
            // inside the lambda (that syntax confuses EF's change tracker and causes DbUpdateConcurrencyException).
            return _context.Orders
                .IgnoreQueryFilters()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x => x.Id == orderId
                         && x.BranchId == branchId
                         && x.Status != OrderStatus.Cancelled
                         && x.Status != OrderStatus.Completed,
                    ct);
        }

        public Task<Order?> GetActiveSessionOrderAsync(string sessionCode, Guid orderId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // Join through QrSessions to find the active session matching the token,
            // then verify the requested order belongs to the same session
            // (same branch + table, created within the session's lifetime).
            // We intentionally do NOT filter on session.ActiveOrderId == orderId so that
            // customers can track ALL their orders placed during a session, not just the latest one.
            return _context.Orders
                .IgnoreQueryFilters()
                .Include(x => x.Items)
                .Where(x => x.Id == orderId
                            && _context.QrSessions.Any(session =>
                                session.SessionToken == sessionCode
                                && session.IsActive
                                && session.ExpiresAt > now
                                && session.BranchId == x.BranchId
                                && session.TableId == x.TableId
                                && x.CreatedAt >= session.CreatedAt
                                && x.CreatedAt <= session.ExpiresAt))
                .FirstOrDefaultAsync(ct);
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
            var now = DateTime.UtcNow;
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Where(x => x.BranchId == branchId
                            && x.TableId == tableId
                            && _context.QrSessions.Any(session => 
                                session.IsActive 
                                && session.TableId == tableId
                                && session.ExpiresAt > now
                                && x.CreatedAt >= session.CreatedAt
                                && x.CreatedAt <= session.ExpiresAt))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<List<QrSession>> GetSessionsByBranchAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.QrSessions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<List<Order>> GetCashierBillOrdersByOrderIdAsync(Guid branchId, Guid orderId, bool includeClosedOrders = false, CancellationToken ct = default)
        {
            var targetOrder = await BuildOrderDetailsQuery()
                .FirstOrDefaultAsync(x => x.Id == orderId, ct);

            if (targetOrder is null || targetOrder.BranchId != branchId)
            {
                return new List<Order>();
            }

            if (!includeClosedOrders
                && (targetOrder.Status == OrderStatus.Cancelled || targetOrder.Status == OrderStatus.Completed))
            {
                return new List<Order> { targetOrder };
            }

            if (!targetOrder.TableId.HasValue)
            {
                return new List<Order> { targetOrder };
            }

            var sessionQuery = _context.QrSessions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId
                            && x.TableId == targetOrder.TableId.Value
                            && targetOrder.CreatedAt >= x.CreatedAt
                            && targetOrder.CreatedAt <= x.ExpiresAt);

            if (!includeClosedOrders)
            {
                var now = DateTime.UtcNow;
                sessionQuery = sessionQuery.Where(x => x.IsActive && x.ExpiresAt > now);
            }

            var session = await sessionQuery
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (session is null)
            {
                return new List<Order> { targetOrder };
            }

            var query = BuildOrderDetailsQuery()
                .Where(x => x.BranchId == branchId
                            && x.TableId == targetOrder.TableId.Value
                            && x.CreatedAt >= session.CreatedAt
                            && x.CreatedAt <= session.ExpiresAt);

            if (includeClosedOrders)
            {
                query = query.Where(x => x.Status != OrderStatus.Cancelled);
            }
            else
            {
                query = query.Where(x => x.Status != OrderStatus.Cancelled
                                         && x.Status != OrderStatus.Completed);
            }

            var billOrders = await query
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);

            return billOrders.Count > 0
                ? billOrders
                : new List<Order> { targetOrder };
        }

        public Task<List<Order>> GetActiveOrdersBySessionCodeAsync(string sessionCode, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Where(x =>
                    _context.QrSessions.Any(session =>
                        session.SessionToken == sessionCode
                        && session.IsActive
                        && session.ExpiresAt > now
                        && session.BranchId == x.BranchId
                        && session.TableId == x.TableId
                        && x.CreatedAt >= session.CreatedAt
                        && x.CreatedAt <= session.ExpiresAt)
                    && x.Status != OrderStatus.Cancelled
                    && x.Status != OrderStatus.Completed)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<int> MarkOrdersCompletedAsync(IEnumerable<Guid> orderIds, DateTime completedAt, CancellationToken ct = default)
        {
            var idList = orderIds.ToList();
            return _context.Orders
                .Where(x => idList.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, OrderStatus.Completed)
                    .SetProperty(x => x.CompletedAt, completedAt)
                    .SetProperty(x => x.UpdatedAt, completedAt), ct);
        }

        public Task AddOrderAsync(Order order, CancellationToken ct = default)
        {
            return _context.Orders.AddAsync(order, ct).AsTask();
        }

        public Task AddOrderItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default)
        {
            // EF Core's ICollection.Add() on a freshly-loaded navigation property does NOT
            // automatically register the new entity as Added in the change tracker.
            // (It works when the parent Order was originally created in the same DbContext
            // lifetime via context.Orders.Add(), but NOT when the Order was re-loaded into a
            // fresh context via GetActiveOrderByIdAsync.)
            // Explicit AddRangeAsync is the safe, reliable path for all scenarios.
            return _context.OrderItems.AddRangeAsync(items, ct);
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
                .IgnoreQueryFilters()
                .Where(x => x.OrderId == orderId && x.Status == PaymentStatus.PENDING)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, PaymentStatus.FAILED)
                    .SetProperty(x => x.UpdatedAt, updatedAt), ct);
        }

        private IQueryable<Order> BuildOrderDetailsQuery()
        {
            return _context.Orders
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Include(x => x.QrSessions);
        }
    }
}
