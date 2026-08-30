using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class FriendshipRepository(ApplicationDbContext dbContext) : IFriendshipRepository
{
    private IQueryable<Friendship> Detailed() => dbContext.Friendships.Include(item => item.Requester).Include(item => item.Addressee);
    public Task<Friendship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Detailed().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<Friendship?> GetBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default) =>
        Detailed().SingleOrDefaultAsync(item => (item.RequesterUserId == firstUserId && item.AddresseeUserId == secondUserId) ||
            (item.RequesterUserId == secondUserId && item.AddresseeUserId == firstUserId), cancellationToken);
    public async Task<IReadOnlyList<Friendship>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await Detailed().Where(item => item.Status == FriendshipStatus.Accepted &&
            (item.RequesterUserId == userId || item.AddresseeUserId == userId)).OrderByDescending(item => item.UpdatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Friendship>> GetPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await Detailed().Where(item => item.Status == FriendshipStatus.Pending &&
            (item.RequesterUserId == userId || item.AddresseeUserId == userId)).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
    public Task<bool> AreFriendsAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default) =>
        dbContext.Friendships.AnyAsync(item => item.Status == FriendshipStatus.Accepted &&
            ((item.RequesterUserId == firstUserId && item.AddresseeUserId == secondUserId) ||
             (item.RequesterUserId == secondUserId && item.AddresseeUserId == firstUserId)), cancellationToken);
    public async Task AddAsync(Friendship entity, CancellationToken cancellationToken = default) => await dbContext.Friendships.AddAsync(entity, cancellationToken);
    public void Remove(Friendship entity) => dbContext.Friendships.Remove(entity);
}
