using GameCollector.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class SyncEventConfiguration : IEntityTypeConfiguration<SyncEvent>
{
    public void Configure(EntityTypeBuilder<SyncEvent> builder)
    {
        builder.ToTable("SyncEvents"); builder.HasKey(item => item.Sequence);
        builder.Property(item => item.Sequence).ValueGeneratedOnAdd();
        builder.Property(item => item.ScopeType).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Operation).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PayloadJson).HasMaxLength(32000).IsRequired();
        builder.HasIndex(item => new { item.ScopeType, item.ScopeId, item.Sequence });
    }
}

public sealed class ProcessedMutationConfiguration : IEntityTypeConfiguration<ProcessedMutation>
{
    public void Configure(EntityTypeBuilder<ProcessedMutation> builder)
    {
        builder.ToTable("ProcessedMutations"); builder.HasKey(item => new { item.UserId, item.MutationId });
        builder.Property(item => item.ResultJson).HasMaxLength(8000).IsRequired();
        builder.HasIndex(item => item.ProcessedAtUtc);
    }
}

public sealed class SyncRetentionStateConfiguration : IEntityTypeConfiguration<SyncRetentionState>
{
    public void Configure(EntityTypeBuilder<SyncRetentionState> builder)
    {
        builder.ToTable("SyncRetentionStates"); builder.HasKey(item => item.ScopeKey);
        builder.Property(item => item.ScopeKey).HasMaxLength(80);
    }
}
