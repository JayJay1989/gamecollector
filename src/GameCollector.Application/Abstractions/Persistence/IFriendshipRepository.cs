using GameCollector.Domain.Users;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IFriendshipRepository : IRepository<Friendship, Guid>
{
    Task<Friendship?> GetBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Friendship>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Friendship>> GetPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> AreFriendsAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
}
