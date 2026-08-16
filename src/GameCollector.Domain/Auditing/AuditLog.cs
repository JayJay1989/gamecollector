using GameCollector.Domain.Common;

namespace GameCollector.Domain.Auditing;

public sealed class AuditLog
{
    private AuditLog() { }
    private AuditLog(Guid id, Guid actorUserId, string action, string entityType, Guid entityId,
        DateTime timestampUtc, string correlationId, Guid? deviceId, string? ipAddress, string? beforeJson, string? afterJson)
    {
        Id = id; ActorUserId = actorUserId; Action = Required(action, 100); EntityType = Required(entityType, 100);
        EntityId = entityId; TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
        CorrelationId = Required(correlationId, 128); DeviceId = deviceId; IpAddress = Optional(ipAddress, 64);
        BeforeJson = Optional(beforeJson, 16000); AfterJson = Optional(afterJson, 16000);
    }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public Guid? DeviceId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public static AuditLog Create(Guid id, Guid actorUserId, string action, string entityType, Guid entityId,
        DateTime timestampUtc, string correlationId, Guid? deviceId, string? ipAddress, string? beforeJson, string? afterJson)
    {
        if (id == Guid.Empty || actorUserId == Guid.Empty || entityId == Guid.Empty) throw new DomainValidationException("Valid audit IDs are required.");
        return new AuditLog(id, actorUserId, action, entityType, entityId, timestampUtc, correlationId, deviceId, ipAddress, beforeJson, afterJson);
    }
    private static string Required(string value, int max) { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainValidationException("An audit field is invalid."); return result; }
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Required(value, max);
}
