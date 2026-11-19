using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Dtos;
using System.Reflection;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Infrastructure.Persistence.Configurations;
using PublishingTracker.Rag.Contracts;
using PublishingTracker.Rag.Entities;
using PublishingTracker.Rag.Persistence;
using PublishingTracker.Rag.Services;

namespace PublishingTracker.Infrastructure.Persistence;

public class PublishingTrackerDbContext : DbContext, IApplicationDbContext, IRagChunkStore
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
    public DbSet<RagDocumentChunk> RagDocumentChunks { get; set; }

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
        modelBuilder.ApplyConfiguration(new RagDocumentChunkConfiguration());

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

        modelBuilder.Entity<RagDocumentChunk>().HasQueryFilter(c =>
            CurrentUserId == null || c.OwnerUserId == null || c.OwnerUserId == CurrentUserId);
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

    // ── IRagChunkStore Implementation ───────────────────────────
    public bool IsPostgreSql => Database.IsNpgsql();

    public Task<List<RagDocumentChunk>> GetByDocumentAsync(
        string documentId,
        int? ownerUserId,
        CancellationToken cancellationToken = default) =>
        RagDocumentChunks
            .Where(c => c.DocumentId == documentId && c.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RagChunkHitDto>> SearchByVectorAsync(
        int authenticatedUserId,
        ReadOnlyMemory<float> queryVector,
        int topK,
        CancellationToken cancellationToken = default)
    {
        VectorMath.ValidateEmbedding(queryVector.Span);

        if (Database.IsNpgsql())
        {
            var vectorLiteral = VectorMath.FormatPgVectorLiteral(queryVector.Span);
            return await Database.SqlQuery<RagChunkHitDto>($"""
                SELECT "DocumentId", "ChunkId", "Title", "Excerpt",
                       (("Embedding"::real[])::vector(1536) <=> CAST({vectorLiteral} AS vector(1536))) AS "CosineDistance"
                FROM "RagDocumentChunks"
                WHERE ("OwnerUserId" IS NULL OR "OwnerUserId" = {authenticatedUserId})
                  AND "Embedding" IS NOT NULL
                ORDER BY (("Embedding"::real[])::vector(1536) <=> CAST({vectorLiteral} AS vector(1536))) ASC, "Id" ASC
                LIMIT {topK}
                """)
                .ToListAsync(cancellationToken);
        }

        var candidates = await RagDocumentChunks
            .AsNoTracking()
            .Where(c => c.OwnerUserId == null || c.OwnerUserId == authenticatedUserId)
            .Select(c => new { c.Id, c.DocumentId, c.ChunkId, c.Title, c.Excerpt, c.Embedding })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(c => VectorMath.TryValidateEmbedding(c.Embedding))
            .Select(c => new
            {
                c.Id,
                Hit = new RagChunkHitDto(
                    c.DocumentId,
                    c.ChunkId,
                    c.Title,
                    c.Excerpt,
                    VectorMath.CosineDistance(c.Embedding, queryVector.Span))
            })
            .OrderBy(entry => entry.Hit.CosineDistance)
            .ThenBy(entry => entry.Id)
            .Take(topK)
            .Select(entry => entry.Hit)
            .ToList();
    }

    public void AddChunks(IEnumerable<RagDocumentChunk> chunks) => RagDocumentChunks.AddRange(chunks);
    public void RemoveChunks(IEnumerable<RagDocumentChunk> chunks) => RagDocumentChunks.RemoveRange(chunks);
}
