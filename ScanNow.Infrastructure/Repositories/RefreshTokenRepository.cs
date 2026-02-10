using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Infrastructure.Repositories
{
    public class RefreshTokenRepository : Repository<RefreshToken, Guid>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token) =>  await _dbSet.FirstOrDefaultAsync(r => r.Token == token);
    }
}
