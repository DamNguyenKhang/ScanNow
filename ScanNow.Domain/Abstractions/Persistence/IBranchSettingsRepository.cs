using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IBranchSettingsRepository
    {
        Task<BranchPaymentConfig?> GetPaymentConfigAsync(Guid branchId, CancellationToken ct = default);
        Task AddPaymentConfigAsync(BranchPaymentConfig config, CancellationToken ct = default);
        Task<List<PaperVoucher>> GetPaperVouchersAsync(Guid branchId, CancellationToken ct = default);
        Task<PaperVoucher?> GetPaperVoucherAsync(Guid branchId, Guid voucherId, CancellationToken ct = default);
        Task<PaperVoucher?> GetPaperVoucherByCodeAsync(Guid branchId, string code, CancellationToken ct = default);
        Task<bool> PaperVoucherCodeExistsAsync(Guid branchId, string code, Guid? excludeVoucherId = null, CancellationToken ct = default);
        Task AddPaperVoucherAsync(PaperVoucher voucher, CancellationToken ct = default);
    }
}
