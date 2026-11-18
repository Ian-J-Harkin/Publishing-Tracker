using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Infrastructure.Persistence.Configurations;

public class EditionConfiguration : IEntityTypeConfiguration<Edition>
{
    public void Configure(EntityTypeBuilder<Edition> builder)
    {
        builder.ToTable("Editions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Format)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ISBN)
            .HasMaxLength(20);

        builder.Property(e => e.BasePrice)
            .HasPrecision(18, 2);

        builder.Property(e => e.Dimensions)
            .HasMaxLength(100);

        builder.Property(e => e.FileSize)
            .HasPrecision(18, 2);

        builder.Property(e => e.Narrator)
            .HasMaxLength(250);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(e => e.Book)
            .WithMany(b => b.Editions)
            .HasForeignKey(e => e.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.PlatformMetadata)
            .WithOne(pm => pm.Edition)
            .HasForeignKey(pm => pm.EditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Sales)
            .WithOne(s => s.Edition)
            .HasForeignKey(s => s.EditionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.BookId);
        builder.HasIndex(e => e.ISBN);
        builder.HasIndex(e => new { e.BookId, e.Format });
    }
}
