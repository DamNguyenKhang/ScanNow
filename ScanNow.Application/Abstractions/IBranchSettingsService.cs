using ScanNow.Application.Features.BranchSettings.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IBranchSettingsService
    {
        Task<BranchPaymentConfigResponse> GetPaymentConfigAsync(Guid branchId);
        Task<BranchPaymentConfigResponse> UpsertPaymentConfigAsync(Guid branchId, UpsertBranchPaymentConfigRequest request);
        Task<List<PaperVoucherResponse>> GetPaperVouchersAsync(Guid branchId);
        Task<PaperVoucherResponse> CreatePaperVoucherAsync(Guid branchId, CreatePaperVoucherRequest request);
        Task<PaperVoucherResponse> UpdatePaperVoucherAsync(Guid branchId, Guid voucherId, UpdatePaperVoucherRequest request);
    }
}
