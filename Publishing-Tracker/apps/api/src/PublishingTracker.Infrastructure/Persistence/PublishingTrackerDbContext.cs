using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Dtos;
using System.Reflection;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Infrastructure.Persistence.Configurations;



    
namespace PublishingTracker.Infrastructure.Persistence;

public class PublishingTrackerDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private int? CurrentUserId => _currentUserService?.UserId;

    public PublishingTrackerDbContext(DbContextOptions<PublishingTrackerDbContext> options) : base(options)
    {
    }

    public PublishingTrackerDbContext(DbContextOptions<PublishingTrackerDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Edition> Editions { get; set; }  // ✅ ADD THIS
    public DbSet<PlatformEditionMetadata> PlatformEditionMetadata { get; set; }  // ✅ ADD THIS
    public DbSet<Sale> Sales { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<ImportJob> ImportJobs { get; set; }

    IQueryable<User> IApplicationDbContext.Users => Users;
    IQueryable<Book> IApplicationDbContext.Books => Books;
    IQueryable<Edition> IApplicationDbContext.Editions => Editions;
    IQueryable<PlatformEditionMetadata> IApplicationDbContext.PlatformEditionMetadata => PlatformEditionMetadata;
    IQueryable<Sale> IApplicationDbContext.Sales => Sales;
    IQueryable<Platform> IApplicationDbContext.Platforms => Platforms;
    IQueryable<ImportJob> IApplicationDbContext.ImportJobs => ImportJobs;

    void IApplicationDbContext.Add<T>(T entity) => Add(entity);
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new BookConfiguration());
        modelBuilder.ApplyConfiguration(new EditionConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformEditionMetadataConfiguration());

        // ═══════════════════════════════════════════════════════════════
        // Sale Configuration
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("Sales");
            entity.HasKey(s => s.Id);
            
            // Properties
            entity.Property(s => s.SaleDate).IsRequired();
            entity.Property(s => s.Quantity).IsRequired();
            entity.Property(s => s.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.Royalty).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.Revenue).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.Currency).IsRequired().HasMaxLength(3);
            entity.Property(s => s.PlatformCommissionRate).HasPrecision(5, 4).IsRequired();
            entity.Property(s => s.ExchangeRate).HasPrecision(18, 6).IsRequired();
            entity.Property(s => s.BaseCurrencyRevenue).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.BaseCurrencyRoyalty).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.OrderId).HasMaxLength(100);
            entity.Property(s => s.CreatedAt).IsRequired();
            
            // Relationships
            entity.HasOne(s => s.Book)
                .WithMany(b => b.Sales)
                .HasForeignKey(s => s.BookId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(s => s.Edition)
                .WithMany(e => e.Sales)
                .HasForeignKey(s => s.EditionId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(s => s.Platform)
                .WithMany(p => p.Sales)
                .HasForeignKey(s => s.PlatformId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Indexes
            entity.HasIndex(s => s.BookId);
            entity.HasIndex(s => s.EditionId);
            entity.HasIndex(s => s.PlatformId);
            entity.HasIndex(s => s.SaleDate);
            entity.HasIndex(s => new { s.BookId, s.SaleDate });
        });

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.Property(e => e.CommissionRate).HasPrecision(5, 4);
        });

        // ── Global Query Filters (Multi-Tenancy) ─────────────────────
        // These filters enforce tenant isolation at the database level.
        // When CurrentUserId is null (e.g. background services),
        // the filter is bypassed so those processes can access all data.
        modelBuilder.Entity<Book>().HasQueryFilter(b =>
            CurrentUserId == null || b.UserId == CurrentUserId);

        modelBuilder.Entity<Edition>().HasQueryFilter(e =>
            CurrentUserId == null || e.Book.UserId == CurrentUserId);

        modelBuilder.Entity<PlatformEditionMetadata>().HasQueryFilter(pm =>
            CurrentUserId == null || pm.Edition.Book.UserId == CurrentUserId);

        modelBuilder.Entity<ImportJob>().HasQueryFilter(j =>
            CurrentUserId == null || j.UserId == CurrentUserId);

        modelBuilder.Entity<Sale>().HasQueryFilter(s =>
            CurrentUserId == null || s.Book.UserId == CurrentUserId);
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
