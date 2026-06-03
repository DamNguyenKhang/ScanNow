using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Repositories
{
    public class BranchSettingsRepository : IBranchSettingsRepository
    {
        private readonly ApplicationDbContext _context;

        public BranchSettingsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<BranchPaymentConfig?> GetPaymentConfigAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.BranchPaymentConfigs.FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        }

        public Task AddPaymentConfigAsync(BranchPaymentConfig config, CancellationToken ct = default)
        {
            return _context.BranchPaymentConfigs.AddAsync(config, ct).AsTask();
        }

        public Task<List<PaperVoucher>> GetPaperVouchersAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.PaperVouchers
                .AsNoTracking()
                .Where(x => x.BranchId == branchId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task<PaperVoucher?> GetPaperVoucherAsync(Guid branchId, Guid voucherId, CancellationToken ct = default)
        {
            return _context.PaperVouchers.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Id == voucherId, ct);
        }

        public Task<PaperVoucher?> GetPaperVoucherByCodeAsync(Guid branchId, string code, CancellationToken ct = default)
        {
            var normalized = code.Trim().ToUpperInvariant();
            return _context.PaperVouchers.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Code.ToUpper() == normalized, ct);
        }

        public Task<bool> PaperVoucherCodeExistsAsync(Guid branchId, string code, Guid? excludeVoucherId = null, CancellationToken ct = default)
        {
            var normalized = code.Trim().ToUpperInvariant();
            return _context.PaperVouchers.AnyAsync(x =>
                x.BranchId == branchId
                && x.Code.ToUpper() == normalized
                && (!excludeVoucherId.HasValue || x.Id != excludeVoucherId.Value), ct);
        }

        public Task AddPaperVoucherAsync(PaperVoucher voucher, CancellationToken ct = default)
        {
            return _context.PaperVouchers.AddAsync(voucher, ct).AsTask();
        }
    }
}
