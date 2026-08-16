using GameCollector.Domain.Common;

namespace GameCollector.Domain.Notifications;

public sealed class Notification
{
    private Notification() { }

    private Notification(Guid id, Guid userId, string type, string payloadJson, DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Type = Required(type, 100);
        PayloadJson = Required(payloadJson, 16000);
        CreatedAtUtc = Utc(createdAtUtc);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime? PushDeliveredAtUtc { get; private set; }
    public DateTime? PushSkippedAtUtc { get; private set; }

    public static Notification Create(Guid id, Guid userId, string type, string payloadJson, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || userId == Guid.Empty) throw new DomainValidationException("Valid notification and user IDs are required.");
        return new Notification(id, userId, type, payloadJson, createdAtUtc);
    }

    public void MarkRead(DateTime readAtUtc) => ReadAtUtc ??= Utc(readAtUtc);
    public void MarkPushDelivered(DateTime deliveredAtUtc) => PushDeliveredAtUtc ??= Utc(deliveredAtUtc);
    public void MarkPushSkipped(DateTime skippedAtUtc) => PushSkippedAtUtc ??= Utc(skippedAtUtc);

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int maximumLength)
    {
        var result = value.Trim();
        if (result.Length is < 1 || result.Length > maximumLength) throw new DomainValidationException("A notification field is invalid.");
        return result;
    }
}
