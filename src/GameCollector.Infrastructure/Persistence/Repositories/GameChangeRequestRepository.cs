using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class GameChangeRequestRepository(ApplicationDbContext dbContext) : IGameChangeRequestRepository
{
    public Task<GameChangeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Detailed().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public async Task AddAsync(GameChangeRequest entity, CancellationToken cancellationToken = default) => await dbContext.GameChangeRequests.AddAsync(entity, cancellationToken);
    public void Remove(GameChangeRequest entity) => dbContext.GameChangeRequests.Remove(entity);
    public Task<bool> HasPendingAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default) => dbContext.GameChangeRequests.AnyAsync(item => item.GameId == gameId && item.ProposedByUserId == userId && item.Status == GameChangeRequestStatus.Pending, cancellationToken);
    public async Task<IReadOnlyList<GameChangeRequest>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) => await Detailed().Where(item => item.ProposedByUserId == userId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<GameChangeRequest>> GetForModerationAsync(GameChangeRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var source = Detailed(); if (status.HasValue) source = source.Where(item => item.Status == status.Value);
        return await source.OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
    }
    private IQueryable<GameChangeRequest> Detailed() => dbContext.GameChangeRequests.Include(item => item.Game);
}
