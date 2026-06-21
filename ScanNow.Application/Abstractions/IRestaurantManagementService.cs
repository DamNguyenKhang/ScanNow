using ScanNow.Application.Features.RestaurantManagement.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IRestaurantManagementService
    {
        Task<PagedResult<RestaurantResponse>> GetRestaurantsAsync(RestaurantQuery query);
        Task<RestaurantResponse> GetRestaurantByIdAsync(Guid id);
        Task<RestaurantResponse> GetRestaurantBySlugAsync(string slug);
        Task<PagedResult<BranchResponse>> GetRestaurantBranchesAsync(Guid restaurantId, BranchQuery query);
        Task<PagedResult<BranchResponse>> GetRestaurantBranchesBySlugAsync(string restaurantSlug, BranchQuery query);
        Task<BranchResponse> GetRestaurantBranchByIdAsync(Guid restaurantId, Guid branchId);
        Task<BranchResponse> GetRestaurantBranchBySlugAsync(string restaurantSlug, string branchSlug);
        Task<RestaurantResponse> CreateRestaurantAsync(CreateRestaurantRequest request);
        Task<RestaurantResponse> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request);
        Task<RestaurantResponse> BanRestaurantAsync(Guid id);
        Task<RestaurantResponse> UnbanRestaurantAsync(Guid id);
        Task<RestaurantResponse?> GetCurrentOwnerRestaurantAsync();
        Task<RestaurantResponse> UpdateCurrentOwnerRestaurantAsync(UpdateRestaurantRequest request);
        Task<BranchResponse> CreateOwnerBranchAsync(CreateBranchRequest request);
        Task<PagedResult<BranchResponse>> GetOwnerBranchesAsync(BranchQuery query);
        Task<BranchResponse> GetOwnerBranchByIdAsync(Guid id);
        Task<BranchResponse> UpdateOwnerBranchAsync(Guid id, UpdateBranchRequest request);
        Task<BranchResponse> InactiveOwnerBranchAsync(Guid id);
        Task<BranchResponse> ActiveOwnerBranchAsync(Guid id);
        Task<IReadOnlyList<BranchResponse>> GetMyBranchesAsync();
        Task<BranchResponse> GetMyBranchByIdAsync(Guid id);
    }
}
