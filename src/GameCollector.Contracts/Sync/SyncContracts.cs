using System.Text.Json;

namespace GameCollector.Contracts.Sync;

public static class SyncMutationTypes
{
    public const string AddCollectionGame = "addCollectionGame";
    public const string RemoveCollectionGame = "removeCollectionGame";
    public const string AddWishlistGame = "addWishlistGame";
    public const string RemoveWishlistGame = "removeWishlistGame";
}

public sealed record SyncMutationDto(Guid MutationId, string Type, Guid GameId, Guid? CollectionId = null);
public sealed record SyncPushRequest(IReadOnlyList<SyncMutationDto> Mutations);
public sealed record SyncMutationResultDto(Guid MutationId, bool Applied, bool Duplicate, long? ServerSequence, string? ErrorCode);
public sealed record SyncPushResponse(IReadOnlyList<SyncMutationResultDto> Results);

public sealed record SyncScopeDto(string Type, Guid? Id, long Cursor);
public sealed record SyncPullRequest(IReadOnlyList<SyncScopeDto> Scopes, int Limit = 500);
public sealed record SyncChangeDto(long Sequence, string ScopeType, Guid? ScopeId, string Operation,
    Guid EntityId, JsonElement Payload, DateTime OccurredAtUtc);
public sealed record SyncScopePageDto(string Type, Guid? Id, long NextCursor, bool HasMore,
    bool IsSnapshot, IReadOnlyList<SyncChangeDto> Changes);
public sealed record SyncPullResponse(IReadOnlyList<SyncScopePageDto> Scopes);
public sealed record SyncBootstrapDto(long Cursor, IReadOnlyList<SyncChangeDto> Snapshot);
