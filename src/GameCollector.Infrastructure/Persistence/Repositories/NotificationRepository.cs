using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(ApplicationDbContext dbContext) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Notifications.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<Notification?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.Where(item => item.UserId == userId && item.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Notification entity, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.AddAsync(entity, cancellationToken);

    public void Remove(Notification entity) => dbContext.Notifications.Remove(entity);
}
