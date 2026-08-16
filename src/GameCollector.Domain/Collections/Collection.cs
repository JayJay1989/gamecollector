using GameCollector.Domain.Common;
using System.Diagnostics.CodeAnalysis;

namespace GameCollector.Domain.Collections;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Collection is the domain's ubiquitous term.")]
public sealed class Collection
{
    private readonly List<CollectionMember> _members = [];

    private Collection()
    {
    }

    private Collection(Guid id, string name, Guid ownerUserId, DateTime createdAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        SetName(name);
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<CollectionMember> Members => _members.AsReadOnly();

    public static Collection Create(Guid id, string name, Guid ownerUserId, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || ownerUserId == Guid.Empty)
        {
            throw new DomainValidationException("Valid collection and owner IDs are required.");
        }

        return new Collection(id, name, ownerUserId, createdAtUtc);
    }

    public void Rename(string name, DateTime updatedAtUtc)
    {
        SetName(name);
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    public bool CanView(Guid userId) => OwnerUserId == userId || _members.Any(member => member.UserId == userId);

    public CollectionRole? GetMemberRole(Guid userId) =>
        _members.SingleOrDefault(member => member.UserId == userId)?.Role;

    public void AddMember(Guid memberId, Guid userId, CollectionRole role, DateTime joinedAtUtc)
    {
        if (userId == OwnerUserId)
        {
            throw new DomainValidationException("The owner cannot also be a membership row.");
        }

        var existing = _members.SingleOrDefault(member => member.UserId == userId);
        if (existing is not null)
        {
            existing.ChangeRole(role);
            return;
        }

        _members.Add(CollectionMember.Create(memberId, Id, userId, role, joinedAtUtc));
    }

    public void ChangeMemberRole(Guid userId, CollectionRole role)
    {
        var member = _members.SingleOrDefault(candidate => candidate.UserId == userId)
            ?? throw new DomainValidationException("The collection member does not exist.");
        member.ChangeRole(role);
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.SingleOrDefault(candidate => candidate.UserId == userId)
            ?? throw new DomainValidationException("The collection member does not exist.");
        _members.Remove(member);
    }

    public void TransferOwnership(Guid newOwnerUserId, bool previousOwnerLeaves, DateTime updatedAtUtc)
    {
        var newOwnerMembership = _members.SingleOrDefault(member => member.UserId == newOwnerUserId)
            ?? throw new DomainValidationException("The new owner must already be a collection member.");
        var previousOwnerId = OwnerUserId;
        _members.Remove(newOwnerMembership);
        OwnerUserId = newOwnerUserId;
        if (!previousOwnerLeaves)
        {
            AddMember(Guid.NewGuid(), previousOwnerId, CollectionRole.Editor, updatedAtUtc);
        }

        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    private void SetName(string name)
    {
        var trimmedName = name.Trim();
        if (trimmedName.Length is < 1 or > 100)
        {
            throw new DomainValidationException("Collection name must contain between 1 and 100 characters.");
        }

        Name = trimmedName;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : value.ToUniversalTime();
}
