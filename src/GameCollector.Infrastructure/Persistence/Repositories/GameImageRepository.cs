using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class GameImageRepository(ApplicationDbContext dbContext) : IGameImageRepository
{
    public Task<GameImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.GameImages.SingleOrDefaultAsync(image => image.Id == id, cancellationToken);
    public Task<GameImage?> GetByGameAndTypeAsync(Guid gameId, GameImageType imageType, CancellationToken cancellationToken = default) =>
        dbContext.GameImages.SingleOrDefaultAsync(image => image.GameId == gameId && image.ImageType == imageType, cancellationToken);
    public async Task<IReadOnlyList<GameImage>> GetForGameAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        await dbContext.GameImages.Where(image => image.GameId == gameId).ToListAsync(cancellationToken);
    public async Task<IReadOnlyDictionary<Guid, Guid>> GetReadyFrontIdsAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken = default) =>
        await dbContext.GameImages
            .Where(image => gameIds.Contains(image.GameId) && image.ImageType == GameImageType.Front && image.Status == GameImageStatus.Ready)
            .ToDictionaryAsync(image => image.GameId, image => image.Id, cancellationToken);
    public async Task<bool> HasReadyFrontAndBackAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        await dbContext.GameImages.Where(image => image.GameId == gameId && image.Status == GameImageStatus.Ready)
            .Select(image => image.ImageType).Distinct().CountAsync(cancellationToken) == 2;
    public async Task AddAsync(GameImage entity, CancellationToken cancellationToken = default) =>
        await dbContext.GameImages.AddAsync(entity, cancellationToken);
    public void Remove(GameImage entity) => dbContext.GameImages.Remove(entity);
}
