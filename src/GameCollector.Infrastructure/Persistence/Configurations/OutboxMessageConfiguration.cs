using GameCollector.Domain.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages"); builder.HasKey(item => item.Id); builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Type).HasMaxLength(100).IsRequired(); builder.Property(item => item.PayloadJson).HasMaxLength(32000).IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(2000);
        builder.HasIndex(item => new { item.ProcessedAtUtc, item.NextAttemptAtUtc });
    }
}
