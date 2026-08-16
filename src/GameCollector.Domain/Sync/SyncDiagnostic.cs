using GameCollector.Domain.Common;

namespace GameCollector.Domain.Sync;

public sealed class SyncDiagnostic
{
    private SyncDiagnostic() { }
    private SyncDiagnostic(Guid id, Guid userId, Guid deviceId)
    { Id = id; UserId = userId; DeviceId = deviceId; }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public DateTime? LastSuccessfulSyncAtUtc { get; private set; }
    public long LastCursor { get; private set; }
    public long UploadedMutations { get; private set; }
    public long DownloadedEvents { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastErrorAtUtc { get; private set; }

    public static SyncDiagnostic Create(Guid id, Guid userId, Guid deviceId)
    {
        if (id == Guid.Empty || userId == Guid.Empty || deviceId == Guid.Empty)
            throw new DomainValidationException("Valid sync diagnostic IDs are required.");
        return new SyncDiagnostic(id, userId, deviceId);
    }

    public void RecordSuccess(long cursor, int uploadedMutations, int downloadedEvents, DateTime occurredAtUtc)
    {
        if (cursor < 0 || uploadedMutations < 0 || downloadedEvents < 0)
            throw new DomainValidationException("Sync diagnostic counters cannot be negative.");
        LastCursor = Math.Max(LastCursor, cursor);
        UploadedMutations += uploadedMutations;
        DownloadedEvents += downloadedEvents;
        LastSuccessfulSyncAtUtc = Utc(occurredAtUtc);
        LastError = null;
        LastErrorAtUtc = null;
    }

    public void RecordFailure(string error, DateTime occurredAtUtc)
    {
        var value = error.Trim();
        if (value.Length is < 1 or > 2000) throw new DomainValidationException("The sync error is invalid.");
        LastError = value;
        LastErrorAtUtc = Utc(occurredAtUtc);
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
