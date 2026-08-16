using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class GameBarcodeConfiguration : IEntityTypeConfiguration<GameBarcode>
{
    public void Configure(EntityTypeBuilder<GameBarcode> builder)
    {
        builder.ToTable("GameBarcodes"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Barcode).HasMaxLength(14).IsRequired();
        builder.Property(item => item.NormalizedBarcode).HasMaxLength(14).IsRequired();
        builder.HasIndex(item => item.NormalizedBarcode).IsUnique();
        builder.HasData(new { Id = CatalogSeedIds.UnoFlipBarcode, GameId = CatalogSeedIds.UnoFlip, Barcode = "887961751062", NormalizedBarcode = "887961751062" });
    }
}
