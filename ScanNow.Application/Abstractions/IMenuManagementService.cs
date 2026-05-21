using ScanNow.Application.Features.MenuManagement.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IMenuManagementService
    {
        Task<PagedResult<CategoryResponse>> GetAdminCategoriesAsync(Guid branchId, CategoryQuery query);
        Task<CategoryResponse> GetAdminCategoryAsync(Guid branchId, Guid id);
        Task<PagedResult<MenuItemResponse>> GetAdminMenuItemsAsync(Guid branchId, MenuQuery query);
        Task<MenuItemResponse> GetAdminMenuItemAsync(Guid id);
        Task<IReadOnlyList<PriceHistoryResponse>> GetAdminPriceHistoryAsync(Guid id);

        Task<PagedResult<CategoryResponse>> GetManageCategoriesAsync(Guid branchId, CategoryQuery query);
        Task<CategoryResponse> GetManageCategoryAsync(Guid branchId, Guid id);
        Task<CategoryResponse> CreateCategoryAsync(Guid branchId, CreateCategoryRequest request);
        Task<CategoryResponse> UpdateCategoryAsync(Guid branchId, Guid id, UpdateCategoryRequest request);
        Task<IReadOnlyList<CategoryResponse>> ReorderCategoriesAsync(Guid branchId, ReorderCategoryRequest request);
        Task<CategoryResponse> SetCategoryActiveAsync(Guid branchId, Guid id, bool isActive);

        Task<PagedResult<MenuItemResponse>> GetManageMenuItemsAsync(Guid branchId, MenuQuery query);
        Task<MenuItemResponse> GetManageMenuItemAsync(Guid id);
        Task<MenuItemResponse> CreateMenuItemAsync(Guid branchId, Guid categoryId, CreateMenuItemRequest request);
        Task<MenuItemResponse> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request);
        Task<MenuItemResponse> SetMenuItemActiveAsync(Guid id, bool isActive);
        Task<IReadOnlyList<MenuItemResponse>> ReorderMenuItemsAsync(Guid branchId, ReorderMenuItemRequest request);
        Task<MenuItemResponse> ToggleMenuItemAvailableAsync(Guid id);
        Task<IReadOnlyList<MenuItemResponse>> BulkUpdateAvailabilityAsync(Guid branchId, BulkAvailabilityRequest request);
        Task<MenuItemResponse> ToggleMenuItemFeaturedAsync(Guid id);
        Task<MenuItemResponse> UpdateMenuItemPriceAsync(Guid id, UpdateMenuItemPriceRequest request);
        Task<IReadOnlyList<PriceHistoryResponse>> GetManagePriceHistoryAsync(Guid id);

        Task<PagedResult<MenuCategoryResponse>> GetMyBranchMenuAsync(Guid branchId, MenuQuery query);
        Task<MenuItemResponse> GetMyMenuItemAsync(Guid id);
        Task<MenuItemResponse> ToggleMyMenuItemAvailableAsync(Guid id);
        Task<IReadOnlyList<MenuItemResponse>> BulkUpdateMyAvailabilityAsync(Guid branchId, BulkAvailabilityRequest request);

        Task<PagedResult<MenuCategoryResponse>> GetPublicBranchMenuAsync(Guid branchId, MenuQuery query);
        Task<MenuItemResponse> GetPublicMenuItemAsync(Guid branchId, Guid id);
    }
}
