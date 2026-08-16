using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class WishlistRepository(ApplicationDbContext dbContext) : IWishlistRepository
{
    public Task<WishlistItem?> GetAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        dbContext.WishlistItems.SingleOrDefaultAsync(item => item.UserId == userId && item.GameId == gameId, cancellationToken);
    public async Task<IReadOnlyList<WishlistItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.WishlistItems.Include(item => item.Game).Where(item => item.UserId == userId && item.IsPresent).OrderBy(item => item.Game.Title).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<WishlistItem>> GetStatesForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.WishlistItems.AsNoTracking().Where(item => item.UserId == userId).OrderBy(item => item.GameId).ToListAsync(cancellationToken);
    public async Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default) => await dbContext.WishlistItems.AddAsync(item, cancellationToken);
    public void Remove(WishlistItem item) => dbContext.WishlistItems.Remove(item);
}
