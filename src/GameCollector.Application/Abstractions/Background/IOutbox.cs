using GameCollector.Domain.Background;

namespace GameCollector.Application.Abstractions.Background;

public static class OutboxMessageTypes
{
    public const string GenerateThumbnail = "GenerateThumbnail";
    public const string SendPushNotification = "SendPushNotification";
}

public interface IOutboxWriter
{
    Task EnqueueAsync(string type, string payloadJson, CancellationToken cancellationToken = default);
}

public interface IOutboxRepository
{
    Task<OutboxMessage?> GetNextDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}

public interface IOutboxMessageHandler
{
    string MessageType { get; }
    Task HandleAsync(string payloadJson, CancellationToken cancellationToken);
}
