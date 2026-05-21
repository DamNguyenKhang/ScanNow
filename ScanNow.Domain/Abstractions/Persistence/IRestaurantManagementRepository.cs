using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IRestaurantManagementRepository
    {
        Task<List<Restaurant>> GetRestaurantsAsync(CancellationToken ct = default);
        Task<Restaurant?> GetRestaurantByIdAsync(Guid id, CancellationToken ct = default);
        Task<Restaurant?> GetRestaurantByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
        Task<bool> RestaurantSlugExistsAsync(string slug, Guid? excludeRestaurantId = null, CancellationToken ct = default);
        Task AddRestaurantAsync(Restaurant restaurant, CancellationToken ct = default);
        Task<List<Branch>> GetBranchesByRestaurantIdAsync(Guid restaurantId, CancellationToken ct = default);
        Task<List<Branch>> GetBranchesByUserIdAsync(Guid userId, CancellationToken ct = default);
        Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchSlugExistsAsync(Guid restaurantId, string slug, Guid? excludeBranchId = null, CancellationToken ct = default);
        Task AddBranchAsync(Branch branch, CancellationToken ct = default);
    }
}
