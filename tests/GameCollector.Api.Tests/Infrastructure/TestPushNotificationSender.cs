using System.Collections.Concurrent;
using GameCollector.Application.Abstractions.Notifications;

namespace GameCollector.Api.Tests.Infrastructure;

public sealed class TestPushNotificationSender : IPushNotificationSender
{
    private readonly ConcurrentQueue<PushDelivery> _deliveries = new();
    private int _failuresToThrow;

    public IReadOnlyCollection<PushDelivery> Deliveries => _deliveries.ToArray();
    public int FailuresToThrow
    {
        get => Volatile.Read(ref _failuresToThrow);
        set => Volatile.Write(ref _failuresToThrow, value);
    }

    public Task<PushSendResult> SendAsync(string fcmToken, Guid notificationId, string type,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _failuresToThrow) >= 0) throw new HttpRequestException("Transient test FCM failure.");
        _deliveries.Enqueue(new PushDelivery(fcmToken, notificationId, type));
        return Task.FromResult(PushSendResult.Sent);
    }
}

public sealed record PushDelivery(string FcmToken, Guid NotificationId, string Type);
