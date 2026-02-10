using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
