using GameCollector.Domain.Common;

namespace GameCollector.Domain.Collections;

public sealed class CollectionMember
{
    private CollectionMember()
    {
    }

    private CollectionMember(Guid id, Guid collectionId, Guid userId, CollectionRole role, DateTime joinedAtUtc)
    {
        Id = id;
        CollectionId = collectionId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc.Kind == DateTimeKind.Utc ? joinedAtUtc : joinedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid CollectionId { get; private set; }
    public Guid UserId { get; private set; }
    public CollectionRole Role { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public Collection Collection { get; private set; } = null!;

    internal static CollectionMember Create(Guid id, Guid collectionId, Guid userId, CollectionRole role, DateTime joinedAtUtc)
    {
        if (id == Guid.Empty || collectionId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainValidationException("Valid membership IDs are required.");
        }

        return new CollectionMember(id, collectionId, userId, role, joinedAtUtc);
    }

    internal void ChangeRole(CollectionRole role) => Role = role;
}
