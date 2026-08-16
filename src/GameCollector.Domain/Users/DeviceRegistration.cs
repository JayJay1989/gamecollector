using GameCollector.Domain.Common;

namespace GameCollector.Domain.Users;

public sealed class DeviceRegistration
{
    private DeviceRegistration()
    {
    }

    private DeviceRegistration(Guid deviceId, Guid userId, string fcmToken, DateTime activatedAtUtc)
    {
        DeviceId = deviceId;
        UserId = userId;
        SetFcmToken(fcmToken);
        ActivatedAtUtc = EnsureUtc(activatedAtUtc);
        LastSeenAtUtc = ActivatedAtUtc;
    }

    public Guid DeviceId { get; private set; }

    public Guid UserId { get; private set; }

    public string FcmToken { get; private set; } = string.Empty;

    public DateTime ActivatedAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public UserProfile User { get; private set; } = null!;

    public static DeviceRegistration Activate(
        Guid deviceId,
        Guid userId,
        string fcmToken,
        DateTime activatedAtUtc)
    {
        if (deviceId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainValidationException("Valid device and user IDs are required.");
        }

        return new DeviceRegistration(deviceId, userId, fcmToken, activatedAtUtc);
    }

    public void Reactivate(string fcmToken, DateTime seenAtUtc)
    {
        SetFcmToken(fcmToken);
        LastSeenAtUtc = EnsureUtc(seenAtUtc);
    }

    public void ActivateForUser(Guid userId, string fcmToken, DateTime activatedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("A valid user ID is required.");
        }

        UserId = userId;
        SetFcmToken(fcmToken);
        ActivatedAtUtc = EnsureUtc(activatedAtUtc);
        LastSeenAtUtc = ActivatedAtUtc;
    }

    private void SetFcmToken(string fcmToken)
    {
        var trimmedToken = fcmToken.Trim();
        if (trimmedToken.Length is < 1 or > 4096)
        {
            throw new DomainValidationException("The FCM token is invalid.");
        }

        FcmToken = trimmedToken;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
