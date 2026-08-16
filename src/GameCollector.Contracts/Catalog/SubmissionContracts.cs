namespace GameCollector.Contracts.Catalog;

public sealed record UpsertGameSubmissionRequest(
    string Title, string? Description, string? Publisher, int? ReleaseYear,
    int? MinimumPlayers, int? MaximumPlayers, int? MinimumAge,
    int? MinimumPlayingTimeMinutes, int? MaximumPlayingTimeMinutes,
    IReadOnlyList<string> Barcodes, IReadOnlyList<Guid> LanguageIds, IReadOnlyList<Guid> TagIds,
    long? ExpectedRevision = null);

public sealed record GameSubmissionDto(GameDto Game, Guid SubmittedByUserId, string? ModerationComment,
    Guid? ApprovedByUserId, DateTime? ApprovedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record ModerateSubmissionRequest(long ExpectedRevision, string? Comment = null);

public sealed record GameChangePatchDto(string? Title = null, string? Description = null,
    string? Publisher = null, int? ReleaseYear = null, int? MinimumPlayers = null,
    int? MaximumPlayers = null, int? MinimumAge = null, int? MinimumPlayingTimeMinutes = null,
    int? MaximumPlayingTimeMinutes = null);

public sealed record CreateGameChangeRequestRequest(GameChangePatchDto ProposedChanges);
public sealed record ReviewGameChangeRequestRequest(long ExpectedGameRevision, string? Comment = null);
public sealed record GameChangeRequestDto(Guid Id, Guid GameId, string GameTitle, Guid ProposedByUserId,
    GameChangePatchDto ProposedChanges, string Status, string? AdminComment, Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
