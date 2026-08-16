using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class GameImageConfiguration : IEntityTypeConfiguration<GameImage>
{
    public void Configure(EntityTypeBuilder<GameImage> builder)
    {
        builder.ToTable("GameImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();
        builder.Property(image => image.OriginalObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(image => image.ThumbnailObjectKey).HasMaxLength(500);
        builder.Property(image => image.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(image => image.Checksum).HasMaxLength(64);
        builder.HasIndex(image => new { image.GameId, image.ImageType }).IsUnique();
        builder.HasIndex(image => image.Status);
        builder.HasOne(image => image.Game).WithMany().HasForeignKey(image => image.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}
