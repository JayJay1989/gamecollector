namespace GameCollector.Contracts.Collections;

public sealed record OwnedGameDto(Guid GameId, string Title, string? Publisher, string ModerationStatus, DateTime AddedAtUtc);
public sealed record WishlistGameDto(Guid GameId, string Title, string? Publisher, string ModerationStatus, DateTime AddedAtUtc);
