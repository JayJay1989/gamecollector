using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.PairKey).HasMaxLength(65).IsRequired();
        builder.HasIndex(item => item.PairKey).IsUnique();
        builder.HasIndex(item => new { item.AddresseeUserId, item.Status });
        builder.HasOne(item => item.Requester).WithMany().HasForeignKey(item => item.RequesterUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Addressee).WithMany().HasForeignKey(item => item.AddresseeUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
