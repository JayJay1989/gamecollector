namespace GameCollector.Domain.Sync;

public sealed class SyncRetentionState
{
    private SyncRetentionState() { }
    public SyncRetentionState(string scopeKey, long minimumCursor)
    { ScopeKey = scopeKey.ToLowerInvariant(); MinimumCursor = minimumCursor; }
    public string ScopeKey { get; private set; } = string.Empty;
    public long MinimumCursor { get; private set; }
    public void Advance(long cursor) { if (cursor > MinimumCursor) MinimumCursor = cursor; }
}
