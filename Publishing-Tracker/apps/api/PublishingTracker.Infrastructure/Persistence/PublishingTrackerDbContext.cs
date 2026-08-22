using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Dtos;
using System.Reflection;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Infrastructure.Persistence;

public class PublishingTrackerDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public PublishingTrackerDbContext(DbContextOptions<PublishingTrackerDbContext> options) : base(options)
    {
    }

    public PublishingTrackerDbContext(DbContextOptions<PublishingTrackerDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<ImportJob> ImportJobs { get; set; }

    IQueryable<User> IApplicationDbContext.Users => Users;
    IQueryable<Book> IApplicationDbContext.Books => Books;
    IQueryable<Sale> IApplicationDbContext.Sales => Sales;
    IQueryable<Platform> IApplicationDbContext.Platforms => Platforms;
    IQueryable<ImportJob> IApplicationDbContext.ImportJobs => ImportJobs;

    void IApplicationDbContext.Add<T>(T entity) => Add(entity);
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.Property(e => e.Revenue).HasPrecision(18, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Royalty).HasPrecision(18, 2);
            entity.Property(e => e.PlatformCommissionRate).HasPrecision(5, 4);
            entity.Property(e => e.ExchangeRate).HasPrecision(18, 6);
            entity.Property(e => e.BaseCurrencyRevenue).HasPrecision(18, 2);
            entity.Property(e => e.BaseCurrencyRoyalty).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.Property(e => e.BasePrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.Property(e => e.CommissionRate).HasPrecision(5, 4);
        });

        // ── Global Query Filters (Multi-Tenancy) ─────────────────────
        // These filters enforce tenant isolation at the database level.
        // When _currentUserService.UserId is null (e.g. background services),
        // the filter is bypassed so those processes can access all data.
        modelBuilder.Entity<Book>().HasQueryFilter(b =>
            _currentUserService == null || _currentUserService.UserId == null || b.UserId == _currentUserService.UserId);

        modelBuilder.Entity<ImportJob>().HasQueryFilter(j =>
            _currentUserService == null || _currentUserService.UserId == null || j.UserId == _currentUserService.UserId);

        modelBuilder.Entity<Sale>().HasQueryFilter(s =>
            _currentUserService == null || _currentUserService.UserId == null || s.Book.UserId == _currentUserService.UserId);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            var properties = entry.Entity.GetType().GetProperties()
                .Where(p => p.IsDefined(typeof(AuditableAttribute), inherit: true));

            foreach (var property in properties)
            {
                var attr = property.GetCustomAttribute<AuditableAttribute>(inherit: true);
                if (attr != null)
                {
                    if (entry.State == EntityState.Added)
                    {
                        SetPropertyValue(property, entry.Entity, now);
                    }
                    else if (entry.State == EntityState.Modified && !attr.IsCreationTime)
                    {
                        SetPropertyValue(property, entry.Entity, now);
                    }
                }
            }
        }

    }

    private static void SetPropertyValue(PropertyInfo property, object entity, DateTime value)
    {
        var setter = property.GetSetMethod(true);
        if (setter == null)
            throw new InvalidOperationException($"Property {property.Name} does not have a setter.");

        setter.Invoke(entity, new object[] { value });
    }
}
