using GameCollector.Application.Common;
using GameCollector.Contracts.Collections;

namespace GameCollector.Application.Collections;

public interface IFriendService
{
    Task<Result<IReadOnlyList<FriendDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FriendRequestDto>>> ListRequestsAsync(CancellationToken cancellationToken = default);
    Task<Result<FriendRequestDto>> SendRequestAsync(CreateFriendRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> RespondAsync(Guid id, bool accept, CancellationToken cancellationToken = default);
    Task<Result<bool>> RemoveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<FriendProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OwnedGameDto>>> GetCollectionGamesAsync(Guid userId, Guid collectionId, CancellationToken cancellationToken = default);
}
