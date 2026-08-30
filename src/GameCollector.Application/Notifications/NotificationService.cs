using System.Text.Json;
using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Background;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Application.Sync;
using GameCollector.Contracts.Notifications;
using GameCollector.Domain.Notifications;

namespace GameCollector.Application.Notifications;

public sealed class NotificationService(
    ICurrentUser currentUser,
    IUserProfileRepository users,
    INotificationRepository notifications,
    IOutboxWriter outbox,
    ISyncEventWriter syncEvents,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : INotificationService, INotificationWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<NotificationDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<NotificationDto>>(ApplicationErrors.ProfileNotFound);
        var items = await notifications.GetForUserAsync(profile.Id, 100, cancellationToken);
        return Result.Success<IReadOnlyList<NotificationDto>>(items.Select(Map).ToList());
    }

    public async Task<Result<bool>> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var item = await notifications.GetForUserAsync(id, profile.Id, cancellationToken);
        if (item is null) return Result.Failure<bool>(ApplicationErrors.NotificationNotFound);
        if (item.ReadAtUtc is null)
        {
            item.MarkRead(Now());
            await WriteSyncEventAsync(item, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return Result.Success(true);
    }

    public async Task<Result<bool>> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var items = await notifications.GetUnreadForUserAsync(profile.Id, cancellationToken);
        var now = Now();
        foreach (var item in items)
        {
            item.MarkRead(now);
            await WriteSyncEventAsync(item, cancellationToken);
        }
        if (items.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var item = await notifications.GetForUserAsync(id, profile.Id, cancellationToken);
        if (item is null) return Result.Failure<bool>(ApplicationErrors.NotificationNotFound);
        notifications.Remove(item);
        await syncEvents.WriteAsync("user", profile.Id, "notificationChanged", id,
            new { Id = id, IsDeleted = true }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    public async Task<Notification> CreateAsync(Guid userId, string type, object payload, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var item = Notification.Create(Guid.NewGuid(), userId, type, JsonSerializer.Serialize(payload, JsonOptions), now);
        await notifications.AddAsync(item, cancellationToken);
        await outbox.EnqueueAsync(OutboxMessageTypes.SendPushNotification,
            JsonSerializer.Serialize(new { NotificationId = item.Id }, JsonOptions), cancellationToken);
        await WriteSyncEventAsync(item, cancellationToken);
        return item;
    }

    private async Task WriteSyncEventAsync(Notification item, CancellationToken cancellationToken) =>
        await syncEvents.WriteAsync("user", item.UserId, "notificationChanged", item.Id,
            new { item.Id, item.Type, Payload = JsonSerializer.Deserialize<JsonElement>(item.PayloadJson, JsonOptions), item.CreatedAtUtc, item.ReadAtUtc }, cancellationToken);

    private Task<Domain.Users.UserProfile?> GetProfileAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(
        currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private static NotificationDto Map(Notification item) => new(item.Id, item.Type,
        JsonSerializer.Deserialize<JsonElement>(item.PayloadJson, JsonOptions), item.CreatedAtUtc, item.ReadAtUtc);
}
