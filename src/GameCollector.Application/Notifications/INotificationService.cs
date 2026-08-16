using GameCollector.Application.Common;
using GameCollector.Contracts.Notifications;
using GameCollector.Domain.Notifications;

namespace GameCollector.Application.Notifications;

public interface INotificationService
{
    Task<Result<IReadOnlyList<NotificationDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> MarkAllReadAsync(CancellationToken cancellationToken = default);
}

public interface INotificationWriter
{
    Task<Notification> CreateAsync(Guid userId, string type, object payload, CancellationToken cancellationToken = default);
}
