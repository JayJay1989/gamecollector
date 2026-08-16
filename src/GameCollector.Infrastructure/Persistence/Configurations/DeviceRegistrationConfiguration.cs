using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameCollector.Infrastructure.Persistence.Configurations;

public sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("DeviceRegistrations");
        builder.HasKey(device => device.DeviceId);
        builder.Property(device => device.DeviceId).ValueGeneratedNever();

        builder.Property(device => device.FcmToken).HasMaxLength(4096).IsRequired();
        builder.Property(device => device.ActivatedAtUtc).IsRequired();
        builder.Property(device => device.LastSeenAtUtc).IsRequired();

        builder.HasIndex(device => device.UserId)
            .IsUnique()
            .HasDatabaseName("UX_DeviceRegistrations_UserId");

        builder.HasOne(device => device.User)
            .WithOne()
            .HasForeignKey<DeviceRegistration>(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
