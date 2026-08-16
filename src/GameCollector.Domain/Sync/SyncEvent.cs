using GameCollector.Domain.Common;

namespace GameCollector.Domain.Sync;

public sealed class SyncEvent
{
    private SyncEvent() { }
    private SyncEvent(string scopeType, Guid? scopeId, string operation, Guid entityId, string payloadJson, DateTime occurredAtUtc)
    {
        ScopeType = Required(scopeType, 30); ScopeId = scopeId; Operation = Required(operation, 100);
        EntityId = entityId; PayloadJson = Required(payloadJson, 32000);
        OccurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc ? occurredAtUtc : occurredAtUtc.ToUniversalTime();
    }
    public long Sequence { get; private set; }
    public string ScopeType { get; private set; } = string.Empty;
    public Guid? ScopeId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public static SyncEvent Create(string scopeType, Guid? scopeId, string operation, Guid entityId, string payloadJson, DateTime occurredAtUtc)
    {
        if (entityId == Guid.Empty) throw new DomainValidationException("A sync entity ID is required.");
        if (string.Equals(scopeType, "catalog", StringComparison.OrdinalIgnoreCase) && scopeId is not null)
            throw new DomainValidationException("Catalog sync events cannot have a scope ID.");
        if (!string.Equals(scopeType, "catalog", StringComparison.OrdinalIgnoreCase) && scopeId is null)
            throw new DomainValidationException("This sync scope requires an ID.");
        return new SyncEvent(scopeType.ToLowerInvariant(), scopeId, operation, entityId, payloadJson, occurredAtUtc);
    }
    private static string Required(string value, int max) { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainValidationException("A sync field is invalid."); return result; }
}
