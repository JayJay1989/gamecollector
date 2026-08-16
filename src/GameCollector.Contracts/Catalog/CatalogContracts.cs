namespace GameCollector.Contracts.Catalog;

public sealed record ReferenceDataDto(Guid Id, string Name, string? Code = null);
public sealed record GameSummaryDto(Guid Id, string Title, string? Publisher, int? ReleaseYear, string ModerationStatus);
public sealed record GameDto(Guid Id, string Title, string? Description, string? Publisher, int? ReleaseYear,
    int? MinimumPlayers, int? MaximumPlayers, int? MinimumAge, int? MinimumPlayingTimeMinutes,
    int? MaximumPlayingTimeMinutes, string ModerationStatus, long Revision,
    IReadOnlyList<string> Barcodes, IReadOnlyList<ReferenceDataDto> Languages, IReadOnlyList<ReferenceDataDto> Tags);
