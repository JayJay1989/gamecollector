using GameCollector.Application.Common;
using GameCollector.Contracts.Collections;

namespace GameCollector.Application.Collections;

public interface IOwnershipService
{
    Task<Result<IReadOnlyList<OwnedGameDto>>> GetCollectionGamesAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<Result<bool>> AddToCollectionAsync(Guid collectionId, Guid gameId, CancellationToken cancellationToken = default);
    Task<Result<bool>> RemoveFromCollectionAsync(Guid collectionId, Guid gameId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WishlistGameDto>>> GetWishlistAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> AddToWishlistAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<Result<bool>> RemoveFromWishlistAsync(Guid gameId, CancellationToken cancellationToken = default);
}
