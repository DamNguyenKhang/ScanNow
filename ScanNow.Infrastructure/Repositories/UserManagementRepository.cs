using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Repositories
{
    public class UserManagementRepository : IUserManagementRepository
    {
        private readonly ApplicationDbContext _context;

        public UserManagementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<Restaurant>> GetRestaurantsByOwnerIdsAsync(IEnumerable<Guid> ownerIds, CancellationToken ct = default)
        {
            var ids = ownerIds.Distinct().ToList();
            return _context.Restaurants
                .AsNoTracking()
                .Where(x => ids.Contains(x.OwnerId))
                .ToListAsync(ct);
        }

        public Task<Restaurant?> GetRestaurantByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
        {
            return _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerId == ownerId, ct);
        }

        public Task<List<Branch>> GetBranchesByRestaurantIdAsync(Guid restaurantId, CancellationToken ct = default)
        {
            return _context.Branches
                .Where(x => x.RestaurantId == restaurantId)
                .ToListAsync(ct);
        }

        public Task<List<Branch>> GetBranchesByManagerIdAsync(Guid managerId, CancellationToken ct = default)
        {
            return _context.Branches
                .Where(x => x.ManagerId == managerId)
                .ToListAsync(ct);
        }

        public Task<List<Branch>> GetBranchesByIdsAsync(IEnumerable<Guid> branchIds, CancellationToken ct = default)
        {
            var ids = branchIds.Distinct().ToList();
            return _context.Branches
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(ct);
        }

        public Task<List<BranchStaff>> GetBranchStaffByBranchIdsAsync(IEnumerable<Guid> branchIds, CancellationToken ct = default)
        {
            var ids = branchIds.Distinct().ToList();
            return _context.BranchStaff
                .Include(x => x.Branch)
                .Where(x => ids.Contains(x.BranchId))
                .ToListAsync(ct);
        }

        public Task<List<BranchStaff>> GetBranchStaffByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return _context.BranchStaff
                .Where(x => x.UserId == userId)
                .ToListAsync(ct);
        }

        public Task AddBranchStaffAsync(BranchStaff branchStaff, CancellationToken ct = default)
        {
            return _context.BranchStaff.AddAsync(branchStaff, ct).AsTask();
        }

        public void RemoveBranchStaffRange(IEnumerable<BranchStaff> branchStaff)
        {
            _context.BranchStaff.RemoveRange(branchStaff);
        }
    }
}
