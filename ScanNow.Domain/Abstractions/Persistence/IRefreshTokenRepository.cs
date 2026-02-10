using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IRefreshTokenRepository : IRepository<RefreshToken, Guid>
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
    }
}
