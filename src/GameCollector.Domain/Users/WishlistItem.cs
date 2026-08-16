using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Domain.Users;

public sealed class WishlistItem
{
    private WishlistItem() { }
    private WishlistItem(Guid id, Guid userId, Guid gameId, DateTime createdAtUtc)
    {
        Id = id; UserId = userId; GameId = gameId;
        CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime();
        ChangedAtUtc = CreatedAtUtc; IsPresent = true;
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsPresent { get; private set; }
    public long LastServerSequence { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public UserProfile User { get; private set; } = null!;
    public Game Game { get; private set; } = null!;
    public static WishlistItem Create(Guid id, Guid userId, Guid gameId, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || userId == Guid.Empty || gameId == Guid.Empty)
            throw new DomainValidationException("Valid wishlist IDs are required.");
        return new WishlistItem(id, userId, gameId, createdAtUtc);
    }
    public void Apply(bool isPresent, DateTime changedAtUtc, long serverSequence)
    {
        if (serverSequence < 0) throw new DomainValidationException("The wishlist change is invalid.");
        IsPresent = isPresent; ChangedAtUtc = changedAtUtc.Kind == DateTimeKind.Utc ? changedAtUtc : changedAtUtc.ToUniversalTime();
        if (isPresent) CreatedAtUtc = ChangedAtUtc;
        LastServerSequence = serverSequence;
    }
}
