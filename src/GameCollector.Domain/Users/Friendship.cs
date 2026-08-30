using GameCollector.Domain.Common;

namespace GameCollector.Domain.Users;

public enum FriendshipStatus { Pending, Accepted, Declined }

public sealed class Friendship
{
    private Friendship() { }
    private Friendship(Guid id, Guid requesterUserId, Guid addresseeUserId, DateTime createdAtUtc)
    {
        Id = id; RequesterUserId = requesterUserId; AddresseeUserId = addresseeUserId;
        PairKey = CreatePairKey(requesterUserId, addresseeUserId);
        Status = FriendshipStatus.Pending; CreatedAtUtc = Utc(createdAtUtc); UpdatedAtUtc = CreatedAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid AddresseeUserId { get; private set; }
    public string PairKey { get; private set; } = string.Empty;
    public FriendshipStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public UserProfile Requester { get; private set; } = null!;
    public UserProfile Addressee { get; private set; } = null!;

    public static Friendship Create(Guid id, Guid requesterUserId, Guid addresseeUserId, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || requesterUserId == Guid.Empty || addresseeUserId == Guid.Empty || requesterUserId == addresseeUserId)
            throw new DomainValidationException("Valid, different users are required for a friend request.");
        return new Friendship(id, requesterUserId, addresseeUserId, createdAtUtc);
    }
    public void Accept(DateTime updatedAtUtc) { EnsurePending(); Status = FriendshipStatus.Accepted; UpdatedAtUtc = Utc(updatedAtUtc); }
    public void Decline(DateTime updatedAtUtc) { EnsurePending(); Status = FriendshipStatus.Declined; UpdatedAtUtc = Utc(updatedAtUtc); }
    private void EnsurePending() { if (Status != FriendshipStatus.Pending) throw new DomainValidationException("The friend request is no longer pending."); }
    private static string CreatePairKey(Guid first, Guid second) => string.CompareOrdinal(first.ToString("N"), second.ToString("N")) < 0
        ? $"{first:N}:{second:N}" : $"{second:N}:{first:N}";
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
