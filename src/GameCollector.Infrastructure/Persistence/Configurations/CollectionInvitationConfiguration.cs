using GameCollector.Domain.Collections;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class CollectionInvitationConfiguration : IEntityTypeConfiguration<CollectionInvitation>
{
    public void Configure(EntityTypeBuilder<CollectionInvitation> builder)
    {
        builder.ToTable("CollectionInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).ValueGeneratedNever();
        builder.HasIndex(invitation => new { invitation.CollectionId, invitation.InviteeUserId })
            .IsUnique().HasFilter("\"Status\" = 0");
        builder.HasOne(invitation => invitation.Collection).WithMany().HasForeignKey(invitation => invitation.CollectionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(invitation => invitation.InviterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(invitation => invitation.InviteeUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
