using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Collections;
using GameCollector.Domain.Collections;
using GameCollector.Domain.Common;
using GameCollector.Domain.Users;
using GameCollector.Application.Sync;
using GameCollector.Application.Notifications;
using GameCollector.Contracts.Notifications;

namespace GameCollector.Application.Collections;

public sealed class CollectionService(
    ICurrentUser currentUser,
    IUserProfileRepository users,
    ICollectionRepository collections,
    ICollectionInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    ISyncEventWriter syncEvents,
    INotificationWriter notificationWriter,
    TimeProvider timeProvider) : ICollectionService
{
    public async Task<Result<IReadOnlyList<CollectionDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<CollectionDto>>(ApplicationErrors.ProfileNotFound);
        if (profile.IsDisabled) return Result.Failure<IReadOnlyList<CollectionDto>>(ApplicationErrors.UserDisabled);
        var items = await collections.GetForUserAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<CollectionDto>>(items.Select(item => Map(item, profile.Id)).ToList());
    }

    public async Task<Result<CollectionDto>> CreateAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<CollectionDto>(ApplicationErrors.ProfileNotFound);
        if (profile.IsDisabled) return Result.Failure<CollectionDto>(ApplicationErrors.UserDisabled);
        try
        {
            var now = Now();
            var collection = Collection.Create(Guid.NewGuid(), request.Name, profile.Id, now);
            await collections.AddAsync(collection, cancellationToken);
            if (profile.DefaultCollectionId is null) profile.SetDefaultCollection(collection.Id, now);
            await syncEvents.WriteAsync("user", profile.Id, "collectionChanged", collection.Id,
                new { collection.Id, collection.Name, collection.OwnerUserId, IsDeleted = false }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(collection, profile.Id));
        }
        catch (DomainValidationException exception) { return Result.Failure<CollectionDto>(ApplicationErrors.Validation(exception.Message)); }
    }

    public async Task<Result<CollectionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessibleAsync(id, cancellationToken);
        return access.Error is not null ? Result.Failure<CollectionDto>(access.Error) : Result.Success(Map(access.Collection!, access.Profile!.Id));
    }

    public async Task<Result<CollectionDto>> UpdateAsync(Guid id, UpdateCollectionRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnerAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<CollectionDto>(access.Error);
        try { access.Collection!.Update(request.Name, request.IsPublic, Now()); await syncEvents.WriteAsync("collection", id, "collectionChanged", id, new { access.Collection.Id, access.Collection.Name, access.Collection.OwnerUserId, access.Collection.IsPublic, IsDeleted = false }, cancellationToken); await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(Map(access.Collection, access.Profile!.Id)); }
        catch (DomainValidationException exception) { return Result.Failure<CollectionDto>(ApplicationErrors.Validation(exception.Message)); }
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnerAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        await syncEvents.WriteAsync("user", access.Profile!.Id, "collectionChanged", id, new { Id = id, IsDeleted = true }, cancellationToken);
        collections.Remove(access.Collection!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    public async Task<Result<bool>> SetDefaultAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessibleAsync(collectionId, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        access.Profile!.SetDefaultCollection(collectionId, Now());
        await syncEvents.WriteAsync("user", access.Profile.Id, "profileChanged", access.Profile.Id,
            new { access.Profile.Id, access.Profile.DefaultCollectionId, access.Profile.UpdatedAtUtc }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    public async Task<Result<CollectionDto>> TransferOwnershipAsync(Guid id, TransferOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnerAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<CollectionDto>(access.Error);
        try
        {
            access.Collection!.TransferOwnership(request.NewOwnerUserId, request.PreviousOwnerLeaves, Now());
            if (request.PreviousOwnerLeaves && access.Profile!.DefaultCollectionId == id) access.Profile.SetDefaultCollection(null, Now());
            await syncEvents.WriteAsync("collection", id, "collectionChanged", id,
                new { access.Collection.Id, access.Collection.Name, access.Collection.OwnerUserId }, cancellationToken);
            await syncEvents.WriteAsync("user", request.NewOwnerUserId, "collectionChanged", id,
                new { access.Collection.Id, access.Collection.Name, access.Collection.OwnerUserId, IsDeleted = false }, cancellationToken);
            if (request.PreviousOwnerLeaves)
                await syncEvents.WriteAsync("user", access.Profile!.Id, "collectionChanged", id, new { Id = id, IsDeleted = true }, cancellationToken);
            await notificationWriter.CreateAsync(request.NewOwnerUserId, NotificationTypes.CollectionMembershipChanged,
                new { CollectionId = id, Role = "Owner" }, cancellationToken);
            if (request.PreviousOwnerLeaves)
                await notificationWriter.CreateAsync(access.Profile!.Id, NotificationTypes.CollectionMembershipRemoved,
                    new { CollectionId = id }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(access.Collection, access.Profile!.Id));
        }
        catch (DomainValidationException exception) { return Result.Failure<CollectionDto>(ApplicationErrors.Validation(exception.Message)); }
    }

    public async Task<Result<IReadOnlyList<CollectionMemberDto>>> GetMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessibleAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<IReadOnlyList<CollectionMemberDto>>(access.Error);
        var collection = access.Collection!;
        var ids = collection.Members.Select(member => member.UserId).Append(collection.OwnerUserId).ToArray();
        var profiles = await users.GetByIdsAsync(ids, cancellationToken);
        var result = profiles.Select(profile => new CollectionMemberDto(profile.Id, profile.DisplayName, profile.Username,
            profile.Id == collection.OwnerUserId ? CollectionMemberRoleDto.Owner : ToDto(collection.GetMemberRole(profile.Id)!.Value),
            collection.Members.SingleOrDefault(member => member.UserId == profile.Id)?.JoinedAtUtc)).ToList();
        return Result.Success<IReadOnlyList<CollectionMemberDto>>(result);
    }

    public async Task<Result<bool>> UpdateMemberAsync(Guid id, Guid userId, UpdateCollectionMemberRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnerAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        var role = FromDto(request.Role);
        if (role is null) return Result.Failure<bool>(ApplicationErrors.InvalidCollectionRole);
        try { access.Collection!.ChangeMemberRole(userId, role.Value); await syncEvents.WriteAsync("collection", id, "membershipChanged", userId, new { UserId = userId, Role = role.Value.ToString(), IsDeleted = false }, cancellationToken); await notificationWriter.CreateAsync(userId, NotificationTypes.CollectionMembershipChanged, new { CollectionId = id, Role = role.Value.ToString() }, cancellationToken); await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(true); }
        catch (DomainValidationException) { return Result.Failure<bool>(ApplicationErrors.MemberNotFound); }
    }

    public async Task<Result<bool>> RemoveMemberAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnerAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        if (access.Collection!.OwnerUserId == userId) return Result.Failure<bool>(ApplicationErrors.OwnerTransferRequired);
        try
        {
            access.Collection.RemoveMember(userId);
            var target = await users.GetByIdAsync(userId, cancellationToken);
            if (target?.DefaultCollectionId == id) target.SetDefaultCollection(null, Now());
            await syncEvents.WriteAsync("collection", id, "membershipChanged", userId, new { UserId = userId, IsDeleted = true }, cancellationToken);
            await syncEvents.WriteAsync("user", userId, "collectionChanged", id, new { Id = id, IsDeleted = true }, cancellationToken);
            await notificationWriter.CreateAsync(userId, NotificationTypes.CollectionMembershipRemoved,
                new { CollectionId = id }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(true);
        }
        catch (DomainValidationException) { return Result.Failure<bool>(ApplicationErrors.MemberNotFound); }
    }

    public async Task<Result<CollectionInvitationDto>> InviteAsync(Guid id, CreateCollectionInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnerAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<CollectionInvitationDto>(access.Error);
        var role = FromDto(request.Role);
        if (role is null) return Result.Failure<CollectionInvitationDto>(ApplicationErrors.InvalidCollectionRole);
        var target = await users.GetByIdAsync(request.InviteeUserId, cancellationToken);
        if (target is null || target.IsDisabled) return Result.Failure<CollectionInvitationDto>(ApplicationErrors.ProfileNotFound);
        if (access.Collection!.CanView(target.Id)) return Result.Failure<CollectionInvitationDto>(ApplicationErrors.Validation("The user already has access to this collection."));
        if (await invitations.HasPendingAsync(id, target.Id, cancellationToken)) return Result.Failure<CollectionInvitationDto>(ApplicationErrors.InvitationAlreadyPending);
        try
        {
            var invitation = CollectionInvitation.Create(Guid.NewGuid(), id, access.Profile!.Id, target.Id, role.Value, Now());
            await invitations.AddAsync(invitation, cancellationToken);
            await syncEvents.WriteAsync("user", target.Id, "invitationChanged", invitation.Id,
                new { invitation.Id, invitation.CollectionId, invitation.InviterUserId, invitation.Role, Status = invitation.Status.ToString() }, cancellationToken);
            await notificationWriter.CreateAsync(target.Id, NotificationTypes.CollectionInvitation,
                new { InvitationId = invitation.Id, CollectionId = id, CollectionName = access.Collection.Name, InviterUserId = access.Profile.Id, Role = invitation.Role.ToString() }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(invitation, access.Collection.Name));
        }
        catch (DomainValidationException exception) { return Result.Failure<CollectionInvitationDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConflictException) { return Result.Failure<CollectionInvitationDto>(ApplicationErrors.InvitationAlreadyPending); }
    }

    public async Task<Result<IReadOnlyList<CollectionInvitationDto>>> GetMyInvitationsAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<CollectionInvitationDto>>(ApplicationErrors.ProfileNotFound);
        var items = await invitations.GetForInviteeAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<CollectionInvitationDto>>(items.Select(item => Map(item, item.Collection.Name)).ToList());
    }

    public async Task<Result<bool>> RespondToInvitationAsync(Guid invitationId, bool accept, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var invitation = await invitations.GetByIdAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.InviteeUserId != profile.Id) return Result.Failure<bool>(ApplicationErrors.InvitationNotFound);
        if (invitation.Status != InvitationStatus.Pending) return Result.Failure<bool>(ApplicationErrors.InvitationNotPending);
        try
        {
            if (accept) { invitation.Accept(Now()); invitation.Collection.AddMember(Guid.NewGuid(), profile.Id, invitation.Role, Now()); if (profile.DefaultCollectionId is null) profile.SetDefaultCollection(invitation.CollectionId, Now()); }
            else invitation.Decline(Now());
            await syncEvents.WriteAsync("user", profile.Id, "invitationChanged", invitation.Id,
                new { invitation.Id, invitation.CollectionId, Status = invitation.Status.ToString() }, cancellationToken);
            if (accept)
            {
                await syncEvents.WriteAsync("user", profile.Id, "collectionChanged", invitation.CollectionId,
                    new { invitation.Collection.Id, invitation.Collection.Name, invitation.Collection.OwnerUserId, IsDeleted = false }, cancellationToken);
                await syncEvents.WriteAsync("collection", invitation.CollectionId, "membershipChanged", profile.Id,
                    new { UserId = profile.Id, Role = invitation.Role.ToString(), IsDeleted = false }, cancellationToken);
            }
            await notificationWriter.CreateAsync(invitation.InviterUserId,
                accept ? NotificationTypes.InvitationAccepted : NotificationTypes.InvitationDeclined,
                new { InvitationId = invitation.Id, CollectionId = invitation.CollectionId, InviteeUserId = profile.Id }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(true);
        }
        catch (DomainValidationException exception) { return Result.Failure<bool>(ApplicationErrors.Validation(exception.Message)); }
    }

    public async Task<Result<IReadOnlyList<UserSearchResultDto>>> SearchUsersAsync(string query, bool username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return Result.Failure<IReadOnlyList<UserSearchResultDto>>(ApplicationErrors.Validation("Search query must contain at least two characters."));
        var results = await users.SearchAsync(query, username, 20, cancellationToken);
        return Result.Success<IReadOnlyList<UserSearchResultDto>>(results.Select(user => new UserSearchResultDto(user.Id, user.DisplayName, user.Username)).ToList());
    }

    private async Task<(UserProfile? Profile, Collection? Collection, ApplicationError? Error)> GetAccessibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return (null, null, ApplicationErrors.ProfileNotFound);
        if (profile.IsDisabled) return (profile, null, ApplicationErrors.UserDisabled);
        var collection = await collections.GetByIdAsync(id, cancellationToken);
        if (collection is null) return (profile, null, ApplicationErrors.CollectionNotFound);
        return collection.CanView(profile.Id) ? (profile, collection, null) : (profile, collection, ApplicationErrors.CollectionAccessDenied);
    }

    private async Task<(UserProfile? Profile, Collection? Collection, ApplicationError? Error)> GetOwnerAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = await GetAccessibleAsync(id, cancellationToken);
        if (access.Error is not null) return access;
        return access.Collection!.OwnerUserId == access.Profile!.Id ? access : (access.Profile, access.Collection, ApplicationErrors.CollectionOwnerRequired);
    }

    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private static CollectionDto Map(Collection item, Guid userId) => new(item.Id, item.Name, item.OwnerUserId, item.OwnerUserId == userId ? CollectionMemberRoleDto.Owner : ToDto(item.GetMemberRole(userId)!.Value), item.IsPublic, item.CreatedAtUtc, item.UpdatedAtUtc);
    private static CollectionInvitationDto Map(CollectionInvitation item, string name) => new(item.Id, item.CollectionId, name, item.InviterUserId, item.InviteeUserId, ToDto(item.Role), item.Status.ToString(), item.CreatedAtUtc);
    private static CollectionMemberRoleDto ToDto(CollectionRole role) => role == CollectionRole.Editor ? CollectionMemberRoleDto.Editor : CollectionMemberRoleDto.Viewer;
    private static CollectionRole? FromDto(CollectionMemberRoleDto role) => role switch { CollectionMemberRoleDto.Editor => CollectionRole.Editor, CollectionMemberRoleDto.Viewer => CollectionRole.Viewer, _ => null };
}
