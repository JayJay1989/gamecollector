using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.UserId, item.GameId }).IsUnique(); builder.HasIndex(item => item.GameId);
        builder.HasIndex(item => new { item.UserId, item.IsPresent });
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Game).WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}
