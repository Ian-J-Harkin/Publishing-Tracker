using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PublishingTracker.Rag.Entities;

namespace PublishingTracker.Infrastructure.Persistence.Configurations;

public sealed class RagDocumentChunkConfiguration : IEntityTypeConfiguration<RagDocumentChunk>
{
    public void Configure(EntityTypeBuilder<RagDocumentChunk> builder)
    {
        builder.ToTable("RagDocumentChunks");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.DocumentId).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Version).IsRequired().HasMaxLength(32);
        builder.Property(c => c.ChunkId).IsRequired().HasMaxLength(160);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Heading).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ChunkIndex).IsRequired();
        builder.Property(c => c.Excerpt).IsRequired().HasMaxLength(2000);
        builder.Property(c => c.ContentHash).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Embedding).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => new { c.DocumentId, c.ChunkId }).IsUnique();
        builder.HasIndex(c => new { c.OwnerUserId, c.DocumentId, c.Version });
    }
}
