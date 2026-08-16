using GameCollector.Domain.Auditing;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Action).HasMaxLength(100).IsRequired(); builder.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); builder.Property(item => item.IpAddress).HasMaxLength(64);
        builder.Property(item => item.BeforeJson).HasMaxLength(16000); builder.Property(item => item.AfterJson).HasMaxLength(16000);
        builder.HasIndex(item => item.TimestampUtc); builder.HasIndex(item => new { item.EntityType, item.EntityId });
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
