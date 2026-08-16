using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Domain.Collections;

public sealed class CollectionGame
{
    private CollectionGame() { }
    private CollectionGame(Guid id, Guid collectionId, Guid gameId, Guid addedByUserId, DateTime addedAtUtc)
    {
        Id = id; CollectionId = collectionId; GameId = gameId; AddedByUserId = addedByUserId;
        AddedAtUtc = addedAtUtc.Kind == DateTimeKind.Utc ? addedAtUtc : addedAtUtc.ToUniversalTime();
        ChangedAtUtc = AddedAtUtc; IsOwned = true;
    }
    public Guid Id { get; private set; }
    public Guid CollectionId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid AddedByUserId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }
    public bool IsOwned { get; private set; }
    public long LastServerSequence { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public Collection Collection { get; private set; } = null!;
    public Game Game { get; private set; } = null!;
    public static CollectionGame Create(Guid id, Guid collectionId, Guid gameId, Guid addedByUserId, DateTime addedAtUtc)
    {
        if (id == Guid.Empty || collectionId == Guid.Empty || gameId == Guid.Empty || addedByUserId == Guid.Empty)
            throw new DomainValidationException("Valid collection-game IDs are required.");
        return new CollectionGame(id, collectionId, gameId, addedByUserId, addedAtUtc);
    }
    public void Apply(bool isOwned, Guid actorUserId, DateTime changedAtUtc, long serverSequence)
    {
        if (actorUserId == Guid.Empty || serverSequence < 0) throw new DomainValidationException("The collection change is invalid.");
        IsOwned = isOwned; AddedByUserId = actorUserId;
        ChangedAtUtc = changedAtUtc.Kind == DateTimeKind.Utc ? changedAtUtc : changedAtUtc.ToUniversalTime();
        if (isOwned) AddedAtUtc = ChangedAtUtc;
        LastServerSequence = serverSequence;
    }
}
