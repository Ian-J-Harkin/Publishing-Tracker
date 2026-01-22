using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Models.Dtos;

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
}