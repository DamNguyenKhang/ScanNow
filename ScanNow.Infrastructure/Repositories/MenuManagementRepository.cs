using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Repositories
{
    public class MenuManagementRepository : IMenuManagementRepository
    {
        private readonly ApplicationDbContext _context;

        public MenuManagementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<Branch?> GetBranchByIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Branches
                .Include(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.Id == branchId, ct);
        }

        public Task<List<Category>> GetCategoriesByBranchIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.Categories
                .Include(x => x.Branch)
                .Where(x => x.BranchId == branchId)
                .ToListAsync(ct);
        }

        public Task<Category?> GetCategoryByIdAsync(Guid categoryId, CancellationToken ct = default)
        {
            return _context.Categories
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.Id == categoryId, ct);
        }

        public Task AddCategoryAsync(Category category, CancellationToken ct = default)
        {
            return _context.Categories.AddAsync(category, ct).AsTask();
        }

        public Task<List<MenuItem>> GetMenuItemsByBranchIdAsync(Guid branchId, CancellationToken ct = default)
        {
            return _context.MenuItems
                .Include(x => x.Category)
                .Include(x => x.Branch)
                .Where(x => x.BranchId == branchId)
                .ToListAsync(ct);
        }

        public Task<MenuItem?> GetMenuItemByIdAsync(Guid menuItemId, CancellationToken ct = default)
        {
            return _context.MenuItems
                .Include(x => x.Category)
                .Include(x => x.Branch)
                .ThenInclude(x => x.Restaurant)
                .FirstOrDefaultAsync(x => x.Id == menuItemId, ct);
        }

        public Task AddMenuItemAsync(MenuItem menuItem, CancellationToken ct = default)
        {
            return _context.MenuItems.AddAsync(menuItem, ct).AsTask();
        }

        public Task<List<MenuItemPriceHistory>> GetPriceHistoriesByMenuItemIdAsync(Guid menuItemId, CancellationToken ct = default)
        {
            return _context.MenuItemPriceHistory
                .AsNoTracking()
                .Include(x => x.ChangedBy)
                .Where(x => x.MenuItemId == menuItemId)
                .OrderByDescending(x => x.ChangedAt)
                .ToListAsync(ct);
        }

        public Task AddPriceHistoryAsync(MenuItemPriceHistory priceHistory, CancellationToken ct = default)
        {
            return _context.MenuItemPriceHistory.AddAsync(priceHistory, ct).AsTask();
        }

        public Task<bool> UserBelongsToBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default)
        {
            return _context.BranchStaff
                .AnyAsync(x => x.UserId == userId && x.BranchId == branchId, ct);
        }
    }
}
