using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly ApplicationDbContext _context;

        public ReportRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<Order>> GetOrdersForBranchesAsync(IEnumerable<Guid> branchIds, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var ids = branchIds.Distinct().ToList();
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Branch)
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Where(x => ids.Contains(x.BranchId) && x.CreatedAt >= from && x.CreatedAt < to)
                .ToListAsync(ct);
        }

        public Task<List<Branch>> GetBranchesWithRestaurantAsync(IEnumerable<Guid> branchIds, CancellationToken ct = default)
        {
            var ids = branchIds.Distinct().ToList();
            return _context.Branches
                .AsNoTracking()
                .Include(x => x.Restaurant)
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(ct);
        }

        public Task<List<Branch>> GetManageableBranchesAsync(Guid userId, bool ownerScope, CancellationToken ct = default)
        {
            var query = _context.Branches
                .AsNoTracking()
                .Include(x => x.Restaurant)
                .AsQueryable();

            query = ownerScope
                ? query.Where(x => x.Restaurant.OwnerId == userId)
                : query.Where(x => x.ManagerId == userId);

            return query.OrderBy(x => x.Name).ToListAsync(ct);
        }

        public Task<int> CountRestaurantsAsync(CancellationToken ct = default)
        {
            return _context.Restaurants.CountAsync(ct);
        }

        public Task<int> CountBranchesAsync(CancellationToken ct = default)
        {
            return _context.Branches.CountAsync(ct);
        }

        public Task<int> CountUsersAsync(CancellationToken ct = default)
        {
            return _context.Users.CountAsync(ct);
        }

        public Task<int> CountOrdersAsync(CancellationToken ct = default)
        {
            return _context.Orders.CountAsync(ct);
        }

        public Task<List<Restaurant>> GetRestaurantsCreatedSinceAsync(DateTime from, CancellationToken ct = default)
        {
            return _context.Restaurants.AsNoTracking().Where(x => x.CreatedAt >= from).ToListAsync(ct);
        }

        public Task<List<ApplicationUser>> GetUsersCreatedSinceAsync(DateTime from, CancellationToken ct = default)
        {
            return _context.Users.AsNoTracking().Where(x => x.CreatedAt >= from).ToListAsync(ct);
        }

        public Task<List<Order>> GetOrdersCreatedSinceAsync(DateTime from, CancellationToken ct = default)
        {
            return _context.Orders
                .AsNoTracking()
                .Include(x => x.Payments)
                .Where(x => x.CreatedAt >= from)
                .ToListAsync(ct);
        }
    }
}
