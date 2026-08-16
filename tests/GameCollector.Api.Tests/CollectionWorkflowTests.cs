using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Users;
using GameCollector.Contracts.Notifications;

namespace GameCollector.Api.Tests;

public sealed class CollectionWorkflowTests(GameCollectorApiFactory factory)
    : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TwoUsersCanShareCollectionAndTransferOwnership()
    {
        var owner = await CreateUserAsync("Owner User");
        var invitee = await CreateUserAsync("Invited User");

        using var createRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/collections", owner);
        createRequest.Content = JsonContent.Create(new CreateCollectionRequest("Our Card Games"));
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var collection = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();
        Assert.NotNull(collection);
        Assert.Equal(CollectionMemberRoleDto.Owner, collection.MyRole);

        using var ownerProfileRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/me", owner);
        using var ownerProfileResponse = await _client.SendAsync(ownerProfileRequest);
        var ownerProfile = await ownerProfileResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal(collection.Id, ownerProfile?.DefaultCollectionId);

        using var inviteRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/collections/{collection.Id}/invitations", owner);
        inviteRequest.Content = JsonContent.Create(new CreateCollectionInvitationRequest(invitee.UserId, CollectionMemberRoleDto.Viewer));
        using var inviteResponse = await _client.SendAsync(inviteRequest);
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<CollectionInvitationDto>();
        Assert.NotNull(invitation);

        using var duplicateInviteRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/collections/{collection.Id}/invitations", owner);
        duplicateInviteRequest.Content = JsonContent.Create(new CreateCollectionInvitationRequest(invitee.UserId, CollectionMemberRoleDto.Viewer));
        using var duplicateInviteResponse = await _client.SendAsync(duplicateInviteRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateInviteResponse.StatusCode);
        Assert.Equal(CollectionErrorCodes.InvitationAlreadyPending, await ErrorCodeAsync(duplicateInviteResponse));

        using var acceptRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/invitations/{invitation.Id}/accept", invitee);
        using var acceptResponse = await _client.SendAsync(acceptRequest);
        Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);
        Assert.Contains(await GetNotificationsAsync(owner), item => item.Type == NotificationTypes.InvitationAccepted);

        using var viewerGetRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/collections/{collection.Id}", invitee);
        using var viewerGetResponse = await _client.SendAsync(viewerGetRequest);
        Assert.Equal(HttpStatusCode.OK, viewerGetResponse.StatusCode);
        var viewerCollection = await viewerGetResponse.Content.ReadFromJsonAsync<CollectionDto>();
        Assert.Equal(CollectionMemberRoleDto.Viewer, viewerCollection?.MyRole);

        using var viewerRenameRequest = Request(HttpMethod.Patch, $"{ApiRoutes.V1}/collections/{collection.Id}", invitee);
        viewerRenameRequest.Content = JsonContent.Create(new UpdateCollectionRequest("Forbidden Rename"));
        using var viewerRenameResponse = await _client.SendAsync(viewerRenameRequest);
        Assert.Equal(HttpStatusCode.Forbidden, viewerRenameResponse.StatusCode);
        Assert.Equal(CollectionErrorCodes.CollectionOwnerRequired, await ErrorCodeAsync(viewerRenameResponse));

        using var roleRequest = Request(HttpMethod.Patch, $"{ApiRoutes.V1}/collections/{collection.Id}/members/{invitee.UserId}", owner);
        roleRequest.Content = JsonContent.Create(new UpdateCollectionMemberRequest(CollectionMemberRoleDto.Editor));
        using var roleResponse = await _client.SendAsync(roleRequest);
        Assert.Equal(HttpStatusCode.NoContent, roleResponse.StatusCode);
        Assert.Contains(await GetNotificationsAsync(invitee), item => item.Type == NotificationTypes.CollectionMembershipChanged);

        using var transferRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/collections/{collection.Id}/transfer-ownership", owner);
        transferRequest.Content = JsonContent.Create(new TransferOwnershipRequest(invitee.UserId, PreviousOwnerLeaves: false));
        using var transferResponse = await _client.SendAsync(transferRequest);
        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);

        using var membersRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/collections/{collection.Id}/members", invitee);
        using var membersResponse = await _client.SendAsync(membersRequest);
        var members = await membersResponse.Content.ReadFromJsonAsync<List<CollectionMemberDto>>();
        Assert.NotNull(members);
        Assert.Single(members, member => member.Role == CollectionMemberRoleDto.Owner);
        Assert.Contains(members, member => member.UserId == owner.UserId && member.Role == CollectionMemberRoleDto.Editor);

        using var oldOwnerRenameRequest = Request(HttpMethod.Patch, $"{ApiRoutes.V1}/collections/{collection.Id}", owner);
        oldOwnerRenameRequest.Content = JsonContent.Create(new UpdateCollectionRequest("Still Forbidden"));
        using var oldOwnerRenameResponse = await _client.SendAsync(oldOwnerRenameRequest);
        Assert.Equal(HttpStatusCode.Forbidden, oldOwnerRenameResponse.StatusCode);

        using var newOwnerRenameRequest = Request(HttpMethod.Patch, $"{ApiRoutes.V1}/collections/{collection.Id}", invitee);
        newOwnerRenameRequest.Content = JsonContent.Create(new UpdateCollectionRequest("Shared Games"));
        using var newOwnerRenameResponse = await _client.SendAsync(newOwnerRenameRequest);
        Assert.Equal(HttpStatusCode.OK, newOwnerRenameResponse.StatusCode);
    }

    [Fact]
    public async Task UserSearchAndInvitationDeclineWork()
    {
        var owner = await CreateUserAsync("Search Owner");
        var invitee = await CreateUserAsync("Search Target");
        using var createRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/collections", owner);
        createRequest.Content = JsonContent.Create(new CreateCollectionRequest("Search Collection"));
        using var createResponse = await _client.SendAsync(createRequest);
        var collection = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();
        Assert.NotNull(collection);

        using var searchRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/users/search?type=username&q={invitee.Username[..5]}", owner);
        using var searchResponse = await _client.SendAsync(searchRequest);
        var found = await searchResponse.Content.ReadFromJsonAsync<List<UserSearchResultDto>>();
        Assert.Contains(found!, user => user.Id == invitee.UserId);

        using var inviteRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/collections/{collection.Id}/invitations", owner);
        inviteRequest.Content = JsonContent.Create(new CreateCollectionInvitationRequest(invitee.UserId, CollectionMemberRoleDto.Editor));
        using var inviteResponse = await _client.SendAsync(inviteRequest);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<CollectionInvitationDto>();
        Assert.NotNull(invitation);

        using var declineRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/invitations/{invitation.Id}/decline", invitee);
        using var declineResponse = await _client.SendAsync(declineRequest);
        Assert.Equal(HttpStatusCode.NoContent, declineResponse.StatusCode);
        Assert.Contains(await GetNotificationsAsync(owner), item => item.Type == NotificationTypes.InvitationDeclined);

        using var collectionRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/collections/{collection.Id}", invitee);
        using var collectionResponse = await _client.SendAsync(collectionRequest);
        Assert.Equal(HttpStatusCode.Forbidden, collectionResponse.StatusCode);
    }

    private async Task<TestUser> CreateUserAsync(string displayName)
    {
        var subject = "subject-" + Guid.NewGuid().ToString("N");
        var username = "u" + Guid.NewGuid().ToString("N")[..12];
        var deviceId = Guid.NewGuid();
        using var onboard = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding", new TestUser(subject, deviceId, Guid.Empty, username));
        onboard.Content = JsonContent.Create(new OnboardUserRequest(displayName, username));
        using var onboardResponse = await _client.SendAsync(onboard);
        Assert.Equal(HttpStatusCode.Created, onboardResponse.StatusCode);
        var profile = await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", new TestUser(subject, deviceId, profile.Id, username));
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(deviceId, "fcm-" + Guid.NewGuid().ToString("N")));
        using var activateResponse = await _client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return new TestUser(subject, deviceId, profile.Id, username);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        if (user.DeviceId != Guid.Empty) request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString());
        return request;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private async Task<List<NotificationDto>> GetNotificationsAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Get, ApiRoutes.V1 + "/me/notifications", user);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<NotificationDto>>())!;
    }

    private sealed record TestUser(string Subject, Guid DeviceId, Guid UserId, string Username);
}
