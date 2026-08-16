using GameCollector.Domain.Notifications;

namespace GameCollector.Application.Abstractions.Persistence;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<Notification?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetUnreadForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
