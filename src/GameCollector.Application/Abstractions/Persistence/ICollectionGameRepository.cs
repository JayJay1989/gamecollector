using GameCollector.Domain.Collections;

namespace GameCollector.Application.Abstractions.Persistence;

public interface ICollectionGameRepository
{
    Task<CollectionGame?> GetAsync(Guid collectionId, Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CollectionGame>> GetForCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CollectionGame>> GetStatesForCollectionsAsync(IReadOnlyCollection<Guid> collectionIds, CancellationToken cancellationToken = default);
    Task AddAsync(CollectionGame item, CancellationToken cancellationToken = default);
    void Remove(CollectionGame item);
}
