using System.Linq.Expressions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain;
namespace PublishingTracker.Application.Interfaces;

/// <summary>
/// A generic repository interface for standard CRUD operations on any <see cref="IEntity"/>.
/// Demonstrates C# Generics with type constraints.
/// </summary>
public interface IRepository<T> where T : class, IEntity
{
    IQueryable<T> Query();
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
