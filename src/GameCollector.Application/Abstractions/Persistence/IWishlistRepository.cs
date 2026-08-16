using GameCollector.Domain.Users;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IWishlistRepository
{
    Task<WishlistItem?> GetAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WishlistItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WishlistItem>> GetStatesForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default);
    void Remove(WishlistItem item);
}
