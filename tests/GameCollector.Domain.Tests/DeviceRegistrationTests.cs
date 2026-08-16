using GameCollector.Domain.Users;

namespace GameCollector.Domain.Tests;

public sealed class DeviceRegistrationTests
{
    [Fact]
    public void ActivateSetsDeviceAndLastSeenTimes()
    {
        var now = DateTime.UtcNow;

        var registration = DeviceRegistration.Activate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fcm-token",
            now);

        Assert.Equal(now, registration.ActivatedAtUtc);
        Assert.Equal(now, registration.LastSeenAtUtc);
    }
}
