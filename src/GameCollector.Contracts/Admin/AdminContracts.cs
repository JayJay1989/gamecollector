using System.Text.Json;
using GameCollector.Contracts.Catalog;

namespace GameCollector.Contracts.Admin;

public sealed record AdminUserSummaryDto(Guid Id, string DisplayName, string Username, bool IsDisabled,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record AdminDeviceDto(Guid DeviceId, DateTime ActivatedAtUtc, DateTime LastSeenAtUtc);
public sealed record AdminUserCollectionDto(Guid Id, string Name, string Access, bool IsOwner);
public sealed record AdminUserDetailDto(AdminUserSummaryDto Profile, AdminDeviceDto? ActiveDevice,
    IReadOnlyList<AdminUserCollectionDto> Collections, IReadOnlyList<GameSubmissionDto> Submissions);

public sealed record AdminCollectionMemberDto(Guid UserId, string DisplayName, string Username, string Role, DateTime? JoinedAtUtc);
public sealed record AdminCollectionGameDto(Guid GameId, string Title, DateTime AddedAtUtc);
public sealed record AdminCollectionSummaryDto(Guid Id, string Name, Guid OwnerUserId, int MemberCount, int GameCount, DateTime UpdatedAtUtc);
public sealed record AdminCollectionDetailDto(Guid Id, string Name, Guid OwnerUserId,
    IReadOnlyList<AdminCollectionMemberDto> Members, IReadOnlyList<AdminCollectionGameDto> Games,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record AdminGameRequest(string Title, string? Description, string? Publisher, int? ReleaseYear,
    int? MinimumPlayers, int? MaximumPlayers, int? MinimumAge, int? MinimumPlayingTimeMinutes,
    int? MaximumPlayingTimeMinutes, IReadOnlyList<string> Barcodes, IReadOnlyList<Guid> LanguageIds,
    IReadOnlyList<Guid> TagIds, long? ExpectedRevision = null);

public sealed record AdminAuditDto(Guid Id, Guid ActorUserId, string Action, string EntityType, Guid EntityId,
    DateTime TimestampUtc, string CorrelationId, Guid? DeviceId, string? IpAddress,
    JsonElement? Before, JsonElement? After);
public sealed record SyncDiagnosticDto(Guid UserId, Guid DeviceId, DateTime? LastSuccessfulSyncAtUtc,
    long LastCursor, long UploadedMutations, long DownloadedEvents, string? LastError, DateTime? LastErrorAtUtc);
