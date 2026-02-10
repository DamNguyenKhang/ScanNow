using ScanNow.Domain.Abstractions.Persistence;

namespace ScanNow.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _dbContext;

        public UnitOfWork(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _dbContext.SaveChangesAsync(ct);
    }
}
