using System.Text.Json;
using GameCollector.Application.Abstractions.Background;
using GameCollector.Application.Abstractions.Notifications;
using GameCollector.Application.Abstractions.Persistence;

namespace GameCollector.Infrastructure.Background;

public sealed class NotificationOutboxHandler(
    INotificationRepository notifications,
    IDeviceRegistrationRepository devices,
    IPushNotificationSender sender,
    TimeProvider timeProvider) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string MessageType => OutboxMessageTypes.SendPushNotification;

    public async Task HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PushPayload>(payloadJson, JsonOptions)
            ?? throw new InvalidDataException("The push-notification outbox payload is invalid.");
        var notification = await notifications.GetByIdAsync(payload.NotificationId, cancellationToken);
        if (notification is null) return;
        if (notification.PushDeliveredAtUtc is not null || notification.PushSkippedAtUtc is not null) return;
        var device = await devices.GetByUserIdAsync(notification.UserId, cancellationToken);
        if (device is null)
        {
            notification.MarkPushSkipped(Now());
            return;
        }
        var result = await sender.SendAsync(device.FcmToken, notification.Id, notification.Type, cancellationToken);
        if (result == PushSendResult.Sent) notification.MarkPushDelivered(Now());
        else notification.MarkPushSkipped(Now());
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private sealed record PushPayload(Guid NotificationId);
}
