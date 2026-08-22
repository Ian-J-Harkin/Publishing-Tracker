namespace PublishingTracker.Application.Common.Interfaces;

public interface IQueryExecutor
{
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}
