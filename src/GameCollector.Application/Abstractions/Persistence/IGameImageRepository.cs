using GameCollector.Domain.Catalog;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IGameImageRepository : IRepository<GameImage, Guid>
{
    Task<GameImage?> GetByGameAndTypeAsync(Guid gameId, GameImageType imageType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameImage>> GetForGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Guid>> GetReadyFrontIdsAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken = default);
    Task<bool> HasReadyFrontAndBackAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<bool> HasReadyFrontAsync(Guid gameId, CancellationToken cancellationToken = default);
}
