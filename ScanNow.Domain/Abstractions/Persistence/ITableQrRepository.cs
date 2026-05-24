using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface ITableQrRepository
    {
        Task<Branch?> GetBranchByIdAsync(Guid branchId, CancellationToken ct = default);
        Task<List<RestaurantTable>> GetTablesByBranchIdAsync(Guid branchId, CancellationToken ct = default);
        Task<RestaurantTable?> GetTableByIdAsync(Guid id, CancellationToken ct = default);
        Task<RestaurantTable?> GetTableByQrCodeTokenAsync(string qrCodeToken, CancellationToken ct = default);
        Task<bool> TableNumberExistsAsync(Guid branchId, string tableNumber, Guid? excludeTableId = null, CancellationToken ct = default);
        Task<bool> QrCodeTokenExistsAsync(string qrCodeToken, CancellationToken ct = default);
        Task AddTableAsync(RestaurantTable table, CancellationToken ct = default);
        Task<QrSession?> GetActiveSessionByTableIdAsync(Guid tableId, CancellationToken ct = default);
        Task<QrSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
        Task<QrSession?> GetActiveSessionByCodeAsync(string sessionCode, CancellationToken ct = default);
        Task<bool> ActiveSessionCodeExistsAsync(string sessionCode, CancellationToken ct = default);
        Task<List<QrSession>> GetSessionsByBranchIdAsync(Guid branchId, CancellationToken ct = default);
        Task AddSessionAsync(QrSession session, CancellationToken ct = default);
        Task<bool> UserBelongsToBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default);
        Task<List<Category>> GetCategoriesByBranchIdAsync(Guid branchId, CancellationToken ct = default);
        Task<List<MenuItem>> GetMenuItemsByBranchIdAsync(Guid branchId, CancellationToken ct = default);
    }
}
