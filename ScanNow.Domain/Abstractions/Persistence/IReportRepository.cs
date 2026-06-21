using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IReportRepository
    {
        Task<List<Order>> GetOrdersForBranchesAsync(IEnumerable<Guid> branchIds, DateTime from, DateTime to, CancellationToken ct = default);
        Task<List<Branch>> GetBranchesWithRestaurantAsync(IEnumerable<Guid> branchIds, CancellationToken ct = default);
        Task<List<Branch>> GetManageableBranchesAsync(Guid userId, bool ownerScope, CancellationToken ct = default);
        Task<int> CountRestaurantsAsync(CancellationToken ct = default);
        Task<int> CountBranchesAsync(CancellationToken ct = default);
        Task<int> CountUsersAsync(CancellationToken ct = default);
        Task<int> CountOrdersAsync(CancellationToken ct = default);
        Task<List<Restaurant>> GetRestaurantsCreatedSinceAsync(DateTime from, CancellationToken ct = default);
        Task<List<ApplicationUser>> GetUsersCreatedSinceAsync(DateTime from, CancellationToken ct = default);
        Task<List<Order>> GetOrdersCreatedSinceAsync(DateTime from, CancellationToken ct = default);
    }
}
