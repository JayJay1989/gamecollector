using GameCollector.Domain.Collections;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("Collections");
        builder.HasKey(collection => collection.Id);
        builder.Property(collection => collection.Id).ValueGeneratedNever();
        builder.Property(collection => collection.Name).HasMaxLength(100).IsRequired();
        builder.Property(collection => collection.CreatedAtUtc).IsRequired();
        builder.Property(collection => collection.UpdatedAtUtc).IsRequired();
        builder.Property(collection => collection.IsPublic).HasDefaultValue(false).IsRequired();
        builder.HasIndex(collection => collection.OwnerUserId);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(collection => collection.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(collection => collection.Members).WithOne(member => member.Collection).HasForeignKey(member => member.CollectionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(collection => collection.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
