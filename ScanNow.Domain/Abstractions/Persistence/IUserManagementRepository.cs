using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IUserManagementRepository
    {
        Task<List<Restaurant>> GetRestaurantsByOwnerIdsAsync(IEnumerable<Guid> ownerIds, CancellationToken ct = default);
        Task<Restaurant?> GetRestaurantByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
        Task<List<Branch>> GetBranchesByRestaurantIdAsync(Guid restaurantId, CancellationToken ct = default);
        Task<List<Branch>> GetBranchesByManagerIdAsync(Guid managerId, CancellationToken ct = default);
        Task<List<Branch>> GetBranchesByIdsAsync(IEnumerable<Guid> branchIds, CancellationToken ct = default);
        Task<List<BranchStaff>> GetBranchStaffByBranchIdsAsync(IEnumerable<Guid> branchIds, CancellationToken ct = default);
        Task<List<BranchStaff>> GetBranchStaffByUserIdAsync(Guid userId, CancellationToken ct = default);
        Task AddBranchStaffAsync(BranchStaff branchStaff, CancellationToken ct = default);
        void RemoveBranchStaffRange(IEnumerable<BranchStaff> branchStaff);
    }
}
