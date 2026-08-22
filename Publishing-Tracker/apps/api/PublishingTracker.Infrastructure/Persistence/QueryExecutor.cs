using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Infrastructure.Persistence;

public sealed class QueryExecutor : IQueryExecutor
{
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.ToListAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.AnyAsync(cancellationToken);
}
