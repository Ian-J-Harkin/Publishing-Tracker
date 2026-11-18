using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Book> Books { get; }
    IQueryable<Edition> Editions { get; }
    IQueryable<PlatformEditionMetadata> PlatformEditionMetadata { get; }
    IQueryable<Sale> Sales { get; }
    IQueryable<Platform> Platforms { get; }
    IQueryable<ImportJob> ImportJobs { get; }

    void Add<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
