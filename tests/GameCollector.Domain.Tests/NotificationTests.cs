using GameCollector.Domain.Common;
using GameCollector.Domain.Notifications;

namespace GameCollector.Domain.Tests;

public sealed class NotificationTests
{
    [Fact]
    public void NotificationKeepsIndependentReadAndPushDeliveryState()
    {
        var created = DateTime.SpecifyKind(new DateTime(2026, 8, 15, 12, 0, 0), DateTimeKind.Utc);
        var item = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "CollectionInvitation", "{}", created);

        item.MarkRead(created.AddMinutes(1));
        item.MarkRead(created.AddMinutes(2));
        item.MarkPushDelivered(created.AddMinutes(3));

        Assert.Equal(created.AddMinutes(1), item.ReadAtUtc);
        Assert.Equal(created.AddMinutes(3), item.PushDeliveredAtUtc);
        Assert.Null(item.PushSkippedAtUtc);
    }

    [Fact]
    public void NotificationRejectsInvalidIdentityAndPayload()
    {
        Assert.Throws<DomainValidationException>(() =>
            Notification.Create(Guid.Empty, Guid.NewGuid(), "Type", "{}", DateTime.UtcNow));
        Assert.Throws<DomainValidationException>(() =>
            Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "Type", new string('x', 16001), DateTime.UtcNow));
    }
}
