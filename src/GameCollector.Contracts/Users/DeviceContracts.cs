using System.ComponentModel.DataAnnotations;

namespace GameCollector.Contracts.Users;

public static class DeviceHeaders
{
    public const string DeviceId = "X-Device-Id";
}

public sealed record ActivateDeviceRequest(
    Guid DeviceId,
    [Required, StringLength(4096, MinimumLength = 1)] string FcmToken);

public sealed record DeviceRegistrationDto(
    Guid DeviceId,
    DateTime ActivatedAtUtc,
    DateTime LastSeenAtUtc);
