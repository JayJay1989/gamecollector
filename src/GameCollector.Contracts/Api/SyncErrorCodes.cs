namespace GameCollector.Contracts.Api;

public static class SyncErrorCodes
{
    public const string InvalidSyncRequest = "invalid_sync_request";
    public const string InvalidSyncScope = "invalid_sync_scope";
    public const string SyncScopeAccessDenied = "sync_scope_access_denied";
    public const string SyncResetRequired = "sync_reset_required";
    public const string UnsupportedMutation = "unsupported_sync_mutation";
}
