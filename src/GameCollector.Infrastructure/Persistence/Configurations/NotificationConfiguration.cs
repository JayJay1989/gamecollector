using GameCollector.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Type).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PayloadJson).HasMaxLength(16000).IsRequired();
        builder.HasIndex(item => new { item.UserId, item.CreatedAtUtc });
        builder.HasOne<GameCollector.Domain.Users.UserProfile>().WithMany()
            .HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
