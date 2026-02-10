using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ScanNow.Infrastructure.Repositories
{
    public class Repository<T, TKey> : IRepository<T, TKey> where T : Entity<TKey>
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        protected IQueryable<T> ApplyIncludes(IQueryable<T> query, Expression<Func<T, object>>[] includes)
        {
            if (includes is { Length: > 0 })
                foreach (var include in includes)
                    query = query.Include(include);

            return query;
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            await _dbSet.AddAsync(entity, ct);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> items, CancellationToken ct = default)
            => await _dbSet.AddRangeAsync(items, ct);

        public virtual Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public virtual Task<T> UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.FromResult(entity);
        }

        public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = ApplyIncludes(_dbSet.AsQueryable(), includes);
            return await query.SingleOrDefaultAsync(x => x.Id.Equals(id), ct);
        }

        public virtual async Task<List<T>> GetAllAsync(CancellationToken ct = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = ApplyIncludes(_dbSet.AsQueryable(), includes);
            return await query.ToListAsync(ct);
        }

        public async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(e => e.Id.Equals(id), cancellationToken);
        }

        public virtual IQueryable<T> Query() => _dbSet.AsQueryable();
    }
}
