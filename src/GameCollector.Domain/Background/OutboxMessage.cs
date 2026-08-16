using GameCollector.Domain.Common;

namespace GameCollector.Domain.Background;

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    private OutboxMessage(Guid id, string type, string payloadJson, DateTime occurredAtUtc)
    { Id = id; Type = Required(type, 100); PayloadJson = Required(payloadJson, 32000); OccurredAtUtc = Utc(occurredAtUtc); NextAttemptAtUtc = OccurredAtUtc; }
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public static OutboxMessage Create(Guid id, string type, string payloadJson, DateTime occurredAtUtc)
    { if (id == Guid.Empty) throw new DomainValidationException("An outbox ID is required."); return new OutboxMessage(id, type, payloadJson, occurredAtUtc); }
    public void Complete(DateTime completedAtUtc) { ProcessedAtUtc = Utc(completedAtUtc); LastError = null; }
    public void Fail(string error, DateTime failedAtUtc)
    {
        Attempts++; LastError = Required(error, 2000);
        NextAttemptAtUtc = Utc(failedAtUtc).AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(Attempts, 8))));
    }
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int max) { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainValidationException("An outbox field is invalid."); return result; }
}
