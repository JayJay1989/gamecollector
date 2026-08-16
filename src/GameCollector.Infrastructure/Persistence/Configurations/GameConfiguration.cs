using GameCollector.Domain.Catalog;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.HasKey(game => game.Id);
        builder.Property(game => game.Id).ValueGeneratedNever();
        builder.Property(game => game.Title).HasMaxLength(200).IsRequired();
        builder.Property(game => game.Description).HasMaxLength(4000);
        builder.Property(game => game.Publisher).HasMaxLength(200);
        builder.Property(game => game.ModerationComment).HasMaxLength(2000);
        builder.Property(game => game.Revision).IsConcurrencyToken();
        builder.HasIndex(game => game.Title);
        builder.HasIndex(game => game.ModerationStatus);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(game => game.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(game => game.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(game => game.Barcodes).WithOne(barcode => barcode.Game).HasForeignKey(barcode => barcode.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(game => game.Languages).WithOne(item => item.Game).HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(game => game.Tags).WithOne(item => item.Game).HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(game => game.Barcodes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(game => game.Languages).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(game => game.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

        var seededAt = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(new
        {
            Id = CatalogSeedIds.UnoFlip, Title = "UNO Flip!", Description = "The classic matching game with a two-sided deck.",
            Publisher = "Mattel", ReleaseYear = (int?)2019, MinimumPlayers = (int?)2, MaximumPlayers = (int?)10,
            MinimumAge = (int?)7, MinimumPlayingTimeMinutes = (int?)15, MaximumPlayingTimeMinutes = (int?)30,
            ModerationStatus = ModerationStatus.Approved, SubmittedByUserId = (Guid?)null,
            CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt, Revision = 1L
        });
    }
}
