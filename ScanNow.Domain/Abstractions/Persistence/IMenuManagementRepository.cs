using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IMenuManagementRepository
    {
        Task<Branch?> GetBranchByIdAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Category>> GetCategoriesByBranchIdAsync(Guid branchId, CancellationToken ct = default);
        Task<Category?> GetCategoryByIdAsync(Guid categoryId, CancellationToken ct = default);
        Task AddCategoryAsync(Category category, CancellationToken ct = default);
        Task<List<MenuItem>> GetMenuItemsByBranchIdAsync(Guid branchId, CancellationToken ct = default);
        Task<MenuItem?> GetMenuItemByIdAsync(Guid menuItemId, CancellationToken ct = default);
        Task AddMenuItemAsync(MenuItem menuItem, CancellationToken ct = default);
        Task<List<MenuItemPriceHistory>> GetPriceHistoriesByMenuItemIdAsync(Guid menuItemId, CancellationToken ct = default);
        Task AddPriceHistoryAsync(MenuItemPriceHistory priceHistory, CancellationToken ct = default);
        Task<bool> UserBelongsToBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default);
    }
}
