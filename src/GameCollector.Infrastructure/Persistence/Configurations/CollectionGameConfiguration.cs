using GameCollector.Domain.Collections;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class CollectionGameConfiguration : IEntityTypeConfiguration<CollectionGame>
{
    public void Configure(EntityTypeBuilder<CollectionGame> builder)
    {
        builder.ToTable("CollectionGames"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.CollectionId, item.GameId }).IsUnique(); builder.HasIndex(item => item.GameId);
        builder.HasIndex(item => new { item.CollectionId, item.IsOwned });
        builder.HasOne(item => item.Collection).WithMany().HasForeignKey(item => item.CollectionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Game).WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(item => item.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
