using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Collections;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class CollectionGameRepository(ApplicationDbContext dbContext) : ICollectionGameRepository
{
    public Task<CollectionGame?> GetAsync(Guid collectionId, Guid gameId, CancellationToken cancellationToken = default) =>
        dbContext.CollectionGames.SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.GameId == gameId, cancellationToken);
    public async Task<IReadOnlyList<CollectionGame>> GetForCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default) =>
        await dbContext.CollectionGames.Include(item => item.Game).Where(item => item.CollectionId == collectionId && item.IsOwned).OrderBy(item => item.Game.Title).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CollectionGame>> GetStatesForCollectionsAsync(IReadOnlyCollection<Guid> collectionIds, CancellationToken cancellationToken = default) =>
        await dbContext.CollectionGames.AsNoTracking().Where(item => collectionIds.Contains(item.CollectionId)).OrderBy(item => item.CollectionId).ThenBy(item => item.GameId).ToListAsync(cancellationToken);
    public async Task AddAsync(CollectionGame item, CancellationToken cancellationToken = default) => await dbContext.CollectionGames.AddAsync(item, cancellationToken);
    public void Remove(CollectionGame item) => dbContext.CollectionGames.Remove(item);
}
