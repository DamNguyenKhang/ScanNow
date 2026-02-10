using ScanNow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IRepository<T, TKey> where T : Entity<TKey>
    {
        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<T> items, CancellationToken ct = default);
        Task<T> UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default, params Expression<Func<T, object>>[] includes);
        Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
        //Task<IEnumerable<T>> GetAllByIdsAsync(IEnumerable<TKey> ids, CancellationToken ct = default, params Expression<Func<T, object>>[] includes);
    }
}
