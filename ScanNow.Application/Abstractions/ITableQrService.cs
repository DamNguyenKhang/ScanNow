using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface ITableQrService
    {
        Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetManageTablesAsync(Guid branchId, TableQuery query);
        Task<TableResponse> GetManageTableAsync(Guid branchId, Guid tableId);
        Task<TableResponse> CreateTableAsync(Guid branchId, CreateTableRequest request);
        Task<TableResponse> UpdateTableAsync(Guid tableId, UpdateTableRequest request);
        Task<TableResponse> UpdateTableStatusAsync(Guid tableId, UpdateTableStatusRequest request);
        Task<TableResponse> ActivateTableAsync(Guid tableId);
        Task<TableResponse> DeactivateTableAsync(Guid tableId);
        Task<TableResponse> RegenerateQrAsync(Guid tableId);
        Task<byte[]> GetQrImageAsync(Guid tableId);
        Task<QrSessionResponse> OpenTableAsync(Guid branchId, Guid tableId);
        Task<QrSessionResponse> CloseSessionAsync(Guid sessionId);
        Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetMyTablesAsync(Guid branchId, TableQuery query);
        Task<TableResponse> GetMyTableAsync(Guid tableId);
        Task<PublicTableResponse> GetPublicTableAsync(string qrCodeToken);
        Task<JoinSessionResponse> JoinSessionAsync(JoinSessionRequest request);
        Task<JoinSessionResponse> JoinSessionByQrTokenAsync(string qrCodeToken);
        Task<SessionMenuResponse> GetSessionMenuAsync(string sessionCode, MenuQuery query);
        Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetAdminTablesAsync(Guid branchId, TableQuery query);
        Task<TableResponse> GetAdminTableAsync(Guid branchId, Guid tableId);
        Task<IReadOnlyList<QrSessionResponse>> GetAdminSessionsAsync(Guid branchId);
    }
}
