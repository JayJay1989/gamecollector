using GameCollector.Domain.Catalog;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class GameChangeRequestConfiguration : IEntityTypeConfiguration<GameChangeRequest>
{
    public void Configure(EntityTypeBuilder<GameChangeRequest> builder)
    {
        builder.ToTable("GameChangeRequests"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ProposedChangesJson).HasMaxLength(16000).IsRequired();
        builder.Property(item => item.AdminComment).HasMaxLength(2000);
        builder.HasIndex(item => new { item.GameId, item.ProposedByUserId }).IsUnique().HasFilter("\"Status\" = 0");
        builder.HasIndex(item => item.Status);
        builder.HasOne(item => item.Game).WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(item => item.ProposedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(item => item.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
