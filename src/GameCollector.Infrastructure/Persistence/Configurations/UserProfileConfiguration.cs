using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameCollector.Domain.Collections;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.IdentitySubject).HasMaxLength(255).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.Username).HasMaxLength(30).IsRequired();
        builder.Property(user => user.NormalizedUsername).HasMaxLength(30).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.Property(user => user.UpdatedAtUtc).IsRequired();

        builder.HasIndex(user => user.IdentitySubject)
            .IsUnique()
            .HasDatabaseName("UX_UserProfiles_IdentitySubject");
        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName("UX_UserProfiles_NormalizedUsername");
        builder.HasOne<Collection>().WithMany().HasForeignKey(user => user.DefaultCollectionId).OnDelete(DeleteBehavior.SetNull);
    }
}
