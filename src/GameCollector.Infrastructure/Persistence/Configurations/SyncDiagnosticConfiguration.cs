using GameCollector.Domain.Sync;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class SyncDiagnosticConfiguration : IEntityTypeConfiguration<SyncDiagnostic>
{
    public void Configure(EntityTypeBuilder<SyncDiagnostic> builder)
    {
        builder.ToTable("SyncDiagnostics");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.LastError).HasMaxLength(2000);
        builder.HasIndex(item => new { item.UserId, item.DeviceId }).IsUnique();
        builder.HasIndex(item => item.LastSuccessfulSyncAtUtc);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
