using GameCollector.Domain.Collections;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class CollectionMemberConfiguration : IEntityTypeConfiguration<CollectionMember>
{
    public void Configure(EntityTypeBuilder<CollectionMember> builder)
    {
        builder.ToTable("CollectionMembers");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();
        builder.HasIndex(member => new { member.CollectionId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
