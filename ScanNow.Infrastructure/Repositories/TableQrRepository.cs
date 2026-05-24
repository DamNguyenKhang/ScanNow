using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Repositories
{
    public class TableQrRepository : ITableQrRepository
    {
        private readonly ApplicationDbContext _context;

        public TableQrRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<Branch?> GetBranchByIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Branches
                .Include(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.Id == branchId, ct);
        }

        public Task<List<RestaurantTable>> GetTablesByBranchIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Tables
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .Include(x => x.QrSessions.Where(session => session.IsActive))
                .Where(x => x.BranchId == branchId)
                .ToListAsync(ct);
        }

        public Task<RestaurantTable?> GetTableByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.Tables
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .Include(x => x.QrSessions.Where(session => session.IsActive))
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<RestaurantTable?> GetTableByQrCodeTokenAsync(string qrCodeToken, CancellationToken ct = default)
        {
            return _context.Tables
                .AsNoTracking()
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.QrCodeToken == qrCodeToken, ct);
        }

        public Task<bool> TableNumberExistsAsync(Guid branchId, string tableNumber, Guid? excludeTableId = null, CancellationToken ct = default)
        {
            return _context.Tables.AnyAsync(x =>
                x.BranchId == branchId
                && x.TableNumber == tableNumber
                && (!excludeTableId.HasValue || x.Id != excludeTableId.Value), ct);
        }

        public Task<bool> QrCodeTokenExistsAsync(string qrCodeToken, CancellationToken ct = default)
        {
            return _context.Tables.AnyAsync(x => x.QrCodeToken == qrCodeToken, ct);
        }

        public Task AddTableAsync(RestaurantTable table, CancellationToken ct = default)
        {
            return _context.Tables.AddAsync(table, ct).AsTask();
        }

        public Task<QrSession?> GetActiveSessionByTableIdAsync(Guid tableId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.QrSessions
                .Include(x => x.Table)
                .Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.TableId == tableId && x.IsActive && x.ExpiresAt > now, ct);
        }

        public Task<QrSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.QrSessions
                .Include(x => x.Table)
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<QrSession?> GetActiveSessionByCodeAsync(string sessionCode, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.QrSessions
                .Include(x => x.Table)
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.SessionToken == sessionCode && x.IsActive && x.ExpiresAt > now, ct);
        }

        public Task<bool> ActiveSessionCodeExistsAsync(string sessionCode, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return _context.QrSessions.AnyAsync(x => x.SessionToken == sessionCode && x.IsActive && x.ExpiresAt > now, ct);
        }

        public Task<List<QrSession>> GetSessionsByBranchIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.QrSessions
                .AsNoTracking()
                .Include(x => x.Table)
                .Include(x => x.Branch)
                .Where(x => x.BranchId == branchId && x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

        public Task AddSessionAsync(QrSession session, CancellationToken ct = default)
        {
            return _context.QrSessions.AddAsync(session, ct).AsTask();
        }

        public async Task<bool> UserBelongsToBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default)
        {
            return await _context.Branches.AnyAsync(x => x.Id == branchId && x.ManagerId == userId, ct)
                || await _context.BranchStaff.AnyAsync(x => x.UserId == userId && x.BranchId == branchId, ct);
        }

        public Task<List<Category>> GetCategoriesByBranchIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Categories
                .Include(x => x.Branch)
                .Where(x => x.BranchId == branchId)
                .ToListAsync(ct);
        }

        public Task<List<MenuItem>> GetMenuItemsByBranchIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.MenuItems
                .Include(x => x.Category)
                .Where(x => x.BranchId == branchId)
                .ToListAsync(ct);
        }
    }
}
