using GameCollector.Application.Common;
using GameCollector.Contracts.Collections;

namespace GameCollector.Application.Collections;

public interface ICollectionService
{
    Task<Result<IReadOnlyList<CollectionDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<CollectionDto>> CreateAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default);
    Task<Result<CollectionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CollectionDto>> UpdateAsync(Guid id, UpdateCollectionRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> SetDefaultAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<Result<CollectionDto>> TransferOwnershipAsync(Guid id, TransferOwnershipRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CollectionMemberDto>>> GetMembersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> UpdateMemberAsync(Guid id, Guid userId, UpdateCollectionMemberRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> RemoveMemberAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<CollectionInvitationDto>> InviteAsync(Guid id, CreateCollectionInvitationRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CollectionInvitationDto>>> GetMyInvitationsAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> RespondToInvitationAsync(Guid invitationId, bool accept, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserSearchResultDto>>> SearchUsersAsync(string query, bool username, CancellationToken cancellationToken = default);
}
