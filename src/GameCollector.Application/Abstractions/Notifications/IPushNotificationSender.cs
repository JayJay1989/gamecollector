namespace GameCollector.Application.Abstractions.Notifications;

public interface IPushNotificationSender
{
    Task<PushSendResult> SendAsync(string fcmToken, Guid notificationId, string type, CancellationToken cancellationToken = default);
}

public enum PushSendResult { Sent, Disabled }
