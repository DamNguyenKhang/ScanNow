using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Repositories
{
    public class RestaurantManagementRepository : IRestaurantManagementRepository
    {
        private readonly ApplicationDbContext _context;

        public RestaurantManagementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<Restaurant>> GetRestaurantsAsync(CancellationToken ct = default)
        {
            return _context.Restaurants
                .AsNoTracking()
                .Include(x => x.Owner)
                .Include(x => x.Branches)
                .ToListAsync(ct);
        }

        public Task<Restaurant?> GetRestaurantByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.Restaurants
                .Include(x => x.Owner)
                .Include(x => x.Branches)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<Restaurant?> GetRestaurantBySlugAsync(string slug, CancellationToken ct = default)
        {
            return _context.Restaurants
                .Include(x => x.Owner)
                .Include(x => x.Branches)
                .FirstOrDefaultAsync(x => x.Slug == slug, ct);
        }

        public Task<Restaurant?> GetRestaurantByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
        {
            return _context.Restaurants
                .Include(x => x.Owner)
                .Include(x => x.Branches)
                .FirstOrDefaultAsync(x => x.OwnerId == ownerId, ct);
        }

        public Task<bool> RestaurantSlugExistsAsync(string slug, Guid? excludeRestaurantId = null, CancellationToken ct = default)
        {
            return _context.Restaurants
                .AnyAsync(x => x.Slug == slug && (!excludeRestaurantId.HasValue || x.Id != excludeRestaurantId.Value), ct);
        }

        public Task AddRestaurantAsync(Restaurant restaurant, CancellationToken ct = default)
        {
            return _context.Restaurants.AddAsync(restaurant, ct).AsTask();
        }

        public Task<List<Branch>> GetBranchesByRestaurantIdAsync(Guid restaurantId, CancellationToken ct = default)
        {
            return _context.Branches
                .AsNoTracking()
                .Include(x => x.Manager)
                .Where(x => x.RestaurantId == restaurantId)
                .ToListAsync(ct);
        }

        public Task<List<Branch>> GetBranchesByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return _context.Branches
                .AsNoTracking()
                .Include(x => x.Manager)
                .Where(branch => branch.ManagerId == userId
                    || _context.BranchStaff.Any(staff => staff.BranchId == branch.Id && staff.UserId == userId))
                .ToListAsync(ct);
        }

        public Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.Branches
                .Include(x => x.Manager)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<Branch?> GetBranchBySlugAsync(Guid restaurantId, string slug, CancellationToken ct = default)
        {
            return _context.Branches
                .Include(x => x.Manager)
                .FirstOrDefaultAsync(x => x.RestaurantId == restaurantId && x.Slug == slug, ct);
        }

        public Task<bool> BranchSlugExistsAsync(Guid restaurantId, string slug, Guid? excludeBranchId = null, CancellationToken ct = default)
        {
            return _context.Branches
                .AnyAsync(x => x.RestaurantId == restaurantId
                    && x.Slug == slug
                    && (!excludeBranchId.HasValue || x.Id != excludeBranchId.Value), ct);
        }

        public Task AddBranchAsync(Branch branch, CancellationToken ct = default)
        {
            return _context.Branches.AddAsync(branch, ct).AsTask();
        }
    }
}
