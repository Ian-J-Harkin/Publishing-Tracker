using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Infrastructure.Persistence.Configurations;

public class PlatformEditionMetadataConfiguration : IEntityTypeConfiguration<PlatformEditionMetadata>
{
    public void Configure(EntityTypeBuilder<PlatformEditionMetadata> builder)
    {
        builder.ToTable("PlatformEditionMetadata");

        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.PlatformProductId)
            .HasMaxLength(100);

        builder.Property(pm => pm.ProductUrl)
            .HasMaxLength(500);

        builder.Property(pm => pm.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pm => pm.MetadataJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(pm => pm.CreatedAt)
            .IsRequired();

        builder.Property(pm => pm.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(pm => pm.Edition)
            .WithMany(e => e.PlatformMetadata)
            .HasForeignKey(pm => pm.EditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.Platform)
            .WithMany()
            .HasForeignKey(pm => pm.PlatformId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(pm => pm.EditionId);
        builder.HasIndex(pm => pm.PlatformId);
        builder.HasIndex(pm => new { pm.EditionId, pm.PlatformId })
            .IsUnique();
        builder.HasIndex(pm => pm.PlatformProductId);
    }
}
