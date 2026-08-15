using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain;

using PublishingTracker.Application.Interfaces;

namespace PublishingTracker.Infrastructure.Persistence.Repositories;

/// <summary>
/// A generic repository implementation using EF Core.
/// Centralizes standard data access logic.
/// </summary>
public class Repository<T> : IRepository<T> where T : class, IEntity
{
    protected readonly PublishingTrackerDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(PublishingTrackerDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(e => e.Id == id, cancellationToken);
    }
}
