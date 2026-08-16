using GameCollector.Domain.Catalog;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IGameChangeRequestRepository : IRepository<GameChangeRequest, Guid>
{
    Task<bool> HasPendingAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameChangeRequest>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameChangeRequest>> GetForModerationAsync(GameChangeRequestStatus? status, CancellationToken cancellationToken = default);
}
