using System.ComponentModel.DataAnnotations;

namespace GameCollector.Contracts.Collections;

public enum CollectionMemberRoleDto { Viewer = 0, Editor = 1, Owner = 2 }

public sealed record CreateCollectionRequest([Required, StringLength(100, MinimumLength = 1)] string Name);
public sealed record UpdateCollectionRequest([Required, StringLength(100, MinimumLength = 1)] string Name);
public sealed record SetDefaultCollectionRequest(Guid CollectionId);
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId, bool PreviousOwnerLeaves);
public sealed record UpdateCollectionMemberRequest(CollectionMemberRoleDto Role);
public sealed record CreateCollectionInvitationRequest(Guid InviteeUserId, CollectionMemberRoleDto Role);

public sealed record CollectionDto(Guid Id, string Name, Guid OwnerUserId, CollectionMemberRoleDto MyRole, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CollectionMemberDto(Guid UserId, string DisplayName, string Username, CollectionMemberRoleDto Role, DateTime? JoinedAtUtc);
public sealed record CollectionInvitationDto(Guid Id, Guid CollectionId, string CollectionName, Guid InviterUserId, Guid InviteeUserId, CollectionMemberRoleDto Role, string Status, DateTime CreatedAtUtc);
public sealed record UserSearchResultDto(Guid Id, string DisplayName, string Username);
