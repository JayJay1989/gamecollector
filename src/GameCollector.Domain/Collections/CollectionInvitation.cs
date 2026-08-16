using GameCollector.Domain.Common;

namespace GameCollector.Domain.Collections;

public enum InvitationStatus { Pending = 0, Accepted = 1, Declined = 2 }

public sealed class CollectionInvitation
{
    private CollectionInvitation() { }

    private CollectionInvitation(Guid id, Guid collectionId, Guid inviterUserId, Guid inviteeUserId, CollectionRole role, DateTime createdAtUtc)
    {
        Id = id;
        CollectionId = collectionId;
        InviterUserId = inviterUserId;
        InviteeUserId = inviteeUserId;
        Role = role;
        Status = InvitationStatus.Pending;
        CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid CollectionId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public Guid InviteeUserId { get; private set; }
    public CollectionRole Role { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public Collection Collection { get; private set; } = null!;

    public static CollectionInvitation Create(Guid id, Guid collectionId, Guid inviterUserId, Guid inviteeUserId, CollectionRole role, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || collectionId == Guid.Empty || inviterUserId == Guid.Empty || inviteeUserId == Guid.Empty || inviterUserId == inviteeUserId)
        {
            throw new DomainValidationException("The collection invitation is invalid.");
        }
        return new CollectionInvitation(id, collectionId, inviterUserId, inviteeUserId, role, createdAtUtc);
    }

    public void Accept(DateTime respondedAtUtc) => Respond(InvitationStatus.Accepted, respondedAtUtc);
    public void Decline(DateTime respondedAtUtc) => Respond(InvitationStatus.Declined, respondedAtUtc);

    private void Respond(InvitationStatus status, DateTime respondedAtUtc)
    {
        if (Status != InvitationStatus.Pending) throw new DomainValidationException("The invitation is no longer pending.");
        Status = status;
        RespondedAtUtc = respondedAtUtc.Kind == DateTimeKind.Utc ? respondedAtUtc : respondedAtUtc.ToUniversalTime();
    }
}
