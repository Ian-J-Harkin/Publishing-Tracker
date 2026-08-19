using Microsoft.EntityFrameworkCore;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Book> Books { get; }
    DbSet<Sale> Sales { get; }
    DbSet<Platform> Platforms { get; }
    DbSet<ImportJob> ImportJobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
