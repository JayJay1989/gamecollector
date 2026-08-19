using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class GameChangeRequestImageConfiguration : IEntityTypeConfiguration<GameChangeRequestImage>
{
    public void Configure(EntityTypeBuilder<GameChangeRequestImage> builder)
    {
        builder.ToTable("GameChangeRequestImages");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Checksum).HasMaxLength(64).IsRequired();
        builder.HasIndex(item => new { item.ChangeRequestId, item.ImageType }).IsUnique();
        builder.HasOne(item => item.ChangeRequest).WithMany(item => item.Images)
            .HasForeignKey(item => item.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
