using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Models.Dtos;
using System.Reflection;
using PublishingTracker.Api.Models;

namespace PublishingTracker.Api.Data;

public class PublishingTrackerDbContext : DbContext
{
    public PublishingTrackerDbContext(DbContextOptions<PublishingTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<Models.User> Users { get; set; }
    public DbSet<Models.Book> Books { get; set; }
    public DbSet<Models.Sale> Sales { get; set; }
    public DbSet<Models.Platform> Platforms { get; set; }
    public DbSet<Models.ImportJob> ImportJobs { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Models.Sale>(entity =>
        {
            entity.Property(e => e.Revenue).HasPrecision(18, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Royalty).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Models.Book>(entity =>
        {
            entity.Property(e => e.BasePrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Models.Platform>(entity =>
        {
            entity.Property(e => e.CommissionRate).HasPrecision(5, 4);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
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
                        property.SetValue(entry.Entity, now);
                    }
                    else if (entry.State == EntityState.Modified && !attr.IsCreationTime)
                    {
                        property.SetValue(entry.Entity, now);
                    }
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}