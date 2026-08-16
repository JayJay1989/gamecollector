using System.Net;
using System.Net.Http.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Notifications;
using GameCollector.Contracts.Users;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class NotificationWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task InvitationNotificationIsPrivateReadableAndRetriedAfterPushFailure()
    {
        var owner = await CreateUserAsync("Notification Owner");
        var invitee = await CreateUserAsync("Notification Invitee");
        var collection = await CreateCollectionAsync(owner, "Notification Collection");
        factory.PushNotifications.FailuresToThrow = 1;
        try
        {
            var invitation = await InviteAsync(owner, invitee.UserId, collection.Id);
            var items = await GetNotificationsAsync(invitee);
            var notification = Assert.Single(items, item => item.Type == NotificationTypes.CollectionInvitation);
            Assert.Equal(invitation.Id, notification.Payload.GetProperty("invitationId").GetGuid());
            Assert.Null(notification.ReadAtUtc);

            using var forbidden = Request(HttpMethod.Post,
                $"{ApiRoutes.V1}/me/notifications/{notification.Id}/read", owner);
            using var forbiddenResponse = await _client.SendAsync(forbidden);
            Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);

            using var read = Request(HttpMethod.Post,
                $"{ApiRoutes.V1}/me/notifications/{notification.Id}/read", invitee);
            using var readResponse = await _client.SendAsync(read);
            Assert.Equal(HttpStatusCode.NoContent, readResponse.StatusCode);
            Assert.NotNull(Assert.Single(await GetNotificationsAsync(invitee), item => item.Id == notification.Id).ReadAtUtc);

            for (var attempt = 0; attempt < 50; attempt++)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var id = notification.Id.ToString();
                var message = await db.OutboxMessages.SingleAsync(item => item.PayloadJson.Contains(id));
                if (message.ProcessedAtUtc is not null)
                {
                    Assert.Equal(1, message.Attempts);
                    Assert.Contains(factory.PushNotifications.Deliveries,
                        delivery => delivery.NotificationId == notification.Id &&
                                    delivery.FcmToken == invitee.FcmToken &&
                                    delivery.Type == NotificationTypes.CollectionInvitation);
                    break;
                }
                if (attempt == 49) Assert.Fail("The failed push notification was not retried.");
                await Task.Delay(100);
            }

            var secondCollection = await CreateCollectionAsync(owner, "Second Notification Collection");
            _ = await InviteAsync(owner, invitee.UserId, secondCollection.Id);
            using var readAll = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/notifications/read-all", invitee);
            using var readAllResponse = await _client.SendAsync(readAll);
            Assert.Equal(HttpStatusCode.NoContent, readAllResponse.StatusCode);
            Assert.All(await GetNotificationsAsync(invitee), item => Assert.NotNull(item.ReadAtUtc));
        }
        finally
        {
            factory.PushNotifications.FailuresToThrow = 0;
        }
    }

    [Fact]
    public async Task DeviceReplacementAndRevocationRemainVisibleInApp()
    {
        var user = await CreateUserAsync("Device Notification User");
        var secondDevice = Guid.NewGuid();
        using var replace = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate",
            user with { DeviceId = secondDevice, FcmToken = "replacement-fcm" });
        replace.Content = JsonContent.Create(new ActivateDeviceRequest(secondDevice, "replacement-fcm"));
        using var replaceResponse = await _client.SendAsync(replace);
        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        var active = user with { DeviceId = secondDevice, FcmToken = "replacement-fcm" };
        Assert.Contains(await GetNotificationsAsync(active), item => item.Type == NotificationTypes.DeviceRegistrationReplaced);

        using var revoke = Request(HttpMethod.Delete, ApiRoutes.V1 + "/me/device", active);
        using var revokeResponse = await _client.SendAsync(revoke);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var thirdDevice = Guid.NewGuid();
        using var reactivate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate",
            active with { DeviceId = thirdDevice, FcmToken = "third-fcm" });
        reactivate.Content = JsonContent.Create(new ActivateDeviceRequest(thirdDevice, "third-fcm"));
        using var reactivateResponse = await _client.SendAsync(reactivate);
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        var current = active with { DeviceId = thirdDevice, FcmToken = "third-fcm" };
        Assert.Contains(await GetNotificationsAsync(current), item => item.Type == NotificationTypes.DeviceRegistrationRevoked);
    }

    private async Task<TestUser> CreateUserAsync(string displayName)
    {
        var user = new TestUser("notification-" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.Empty,
            "fcm-" + Guid.NewGuid().ToString("N"));
        using var onboard = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding", user);
        onboard.Content = JsonContent.Create(new OnboardUserRequest(displayName, "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard);
        Assert.Equal(HttpStatusCode.Created, onboardResponse.StatusCode);
        var profile = await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        user = user with { UserId = profile.Id };
        using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(user.DeviceId, user.FcmToken));
        using var activateResponse = await _client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return user;
    }

    private async Task<CollectionDto> CreateCollectionAsync(TestUser owner, string name)
    {
        using var request = Request(HttpMethod.Post, ApiRoutes.V1 + "/collections", owner);
        request.Content = JsonContent.Create(new CreateCollectionRequest(name));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CollectionDto>())!;
    }

    private async Task<CollectionInvitationDto> InviteAsync(TestUser owner, Guid inviteeId, Guid collectionId)
    {
        using var request = Request(HttpMethod.Post, $"{ApiRoutes.V1}/collections/{collectionId}/invitations", owner);
        request.Content = JsonContent.Create(new CreateCollectionInvitationRequest(inviteeId, CollectionMemberRoleDto.Viewer));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CollectionInvitationDto>())!;
    }

    private async Task<List<NotificationDto>> GetNotificationsAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Get, ApiRoutes.V1 + "/me/notifications", user);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<NotificationDto>>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        if (user.DeviceId != Guid.Empty) request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString());
        return request;
    }

    private sealed record TestUser(string Subject, Guid DeviceId, Guid UserId, string FcmToken);
}
