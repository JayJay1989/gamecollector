using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("Languages"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Code).HasMaxLength(10).IsRequired(); builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique(); builder.HasIndex(item => item.Name).IsUnique();
        builder.HasData(new Language(CatalogSeedIds.English, "en", "English"), new Language(CatalogSeedIds.Dutch, "nl", "Dutch"), new Language(CatalogSeedIds.French, "fr", "French"), new Language(CatalogSeedIds.German, "de", "German"));
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(100).IsRequired(); builder.HasIndex(item => item.Name).IsUnique();
        builder.HasData(new Tag(CatalogSeedIds.CardGame, "Card Game"), new Tag(CatalogSeedIds.Family, "Family"), new Tag(CatalogSeedIds.Party, "Party"), new Tag(CatalogSeedIds.Strategy, "Strategy"), new Tag(CatalogSeedIds.Cooperative, "Cooperative"), new Tag(CatalogSeedIds.Fast, "Fast"), new Tag(CatalogSeedIds.TwoPlayer, "Two Player"));
    }
}

public sealed class GameLanguageConfiguration : IEntityTypeConfiguration<GameLanguage>
{
    public void Configure(EntityTypeBuilder<GameLanguage> builder)
    {
        builder.ToTable("GameLanguages"); builder.HasKey(item => new { item.GameId, item.LanguageId });
        builder.HasOne(item => item.Language).WithMany().HasForeignKey(item => item.LanguageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new GameLanguage(CatalogSeedIds.UnoFlip, CatalogSeedIds.English));
    }
}

public sealed class GameTagConfiguration : IEntityTypeConfiguration<GameTag>
{
    public void Configure(EntityTypeBuilder<GameTag> builder)
    {
        builder.ToTable("GameTags"); builder.HasKey(item => new { item.GameId, item.TagId });
        builder.HasOne(item => item.Tag).WithMany().HasForeignKey(item => item.TagId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new GameTag(CatalogSeedIds.UnoFlip, CatalogSeedIds.CardGame), new GameTag(CatalogSeedIds.UnoFlip, CatalogSeedIds.Family));
    }
}
