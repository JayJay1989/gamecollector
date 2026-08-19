using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using GameCollector.Contracts.Users;
using GameCollector.Contracts.Notifications;
using GameCollector.Domain.Auditing;
using GameCollector.Domain.Catalog;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class ModerationWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task UserCanListEditAndDeleteOwnDraftSubmission()
    {
        var submitter = await CreateUserAsync("Draft Owner");
        using var createRequest = UserRequest(HttpMethod.Post, ApiRoutes.V1 + "/game-submissions", submitter);
        createRequest.Content = JsonContent.Create(Submission("Disposable Draft"));
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();

        using var listRequest = UserRequest(HttpMethod.Get, ApiRoutes.V1 + "/game-submissions/mine", submitter);
        using var listResponse = await _client.SendAsync(listRequest);
        var mine = await listResponse.Content.ReadFromJsonAsync<List<GameSubmissionDto>>();
        Assert.Contains(mine!, item => item.Game.Id == created!.Game.Id && item.Game.ModerationStatus == "Draft");

        using var updateRequest = UserRequest(HttpMethod.Put, $"{ApiRoutes.V1}/game-submissions/{created!.Game.Id}", submitter);
        updateRequest.Content = JsonContent.Create(Submission("Edited Draft", created.Game.Revision));
        using var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var deleteRequest = UserRequest(HttpMethod.Delete, $"{ApiRoutes.V1}/game-submissions/{created.Game.Id}", submitter);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getRequest = UserRequest(HttpMethod.Get, $"{ApiRoutes.V1}/game-submissions/{created.Game.Id}", submitter);
        using var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task SubmissionMovesThroughChangesAndApprovalWithAuditHistory()
    {
        var submitter = await CreateUserAsync("Submitter");
        var admin = await CreateUserAsync("Administrator", activateDevice: false);
        var other = await CreateUserAsync("Other User");
        using var createRequest = UserRequest(HttpMethod.Post, ApiRoutes.V1 + "/game-submissions", submitter);
        createRequest.Content = JsonContent.Create(Submission("Review Me"));
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();
        Assert.Equal("Draft", created?.Game.ModerationStatus);

        using var hiddenRequest = UserRequest(HttpMethod.Get, $"{ApiRoutes.V1}/games/{created!.Game.Id}", other);
        using var hiddenResponse = await _client.SendAsync(hiddenRequest);
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);

        using var prematureRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.V1}/game-submissions/{created.Game.Id}/submit", submitter);
        using var prematureResponse = await _client.SendAsync(prematureRequest);
        Assert.Equal(HttpStatusCode.Conflict, prematureResponse.StatusCode);
        Assert.Equal(ModerationErrorCodes.RequiredImagesMissing, await ErrorCodeAsync(prematureResponse));

        await AddReadyImagesAsync(created.Game.Id);
        using var submitRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.V1}/game-submissions/{created.Game.Id}/submit", submitter);
        using var submitResponse = await _client.SendAsync(submitRequest);
        var pending = await submitResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();
        Assert.Equal("Pending", pending?.Game.ModerationStatus);

        using var changesRequest = AdminRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/submissions/{created.Game.Id}/needs-changes", admin);
        changesRequest.Headers.Add("X-Correlation-ID", "moderation-needs-changes");
        changesRequest.Content = JsonContent.Create(new ModerateSubmissionRequest(pending!.Game.Revision, "Please clarify the title."));
        using var changesResponse = await _client.SendAsync(changesRequest);
        Assert.Equal(HttpStatusCode.OK, changesResponse.StatusCode);
        var needsChanges = await changesResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();
        Assert.Equal("NeedsChanges", needsChanges?.Game.ModerationStatus);
        Assert.Equal("Please clarify the title.", needsChanges?.ModerationComment);
        Assert.Contains(await GetNotificationsAsync(submitter), item => item.Type == NotificationTypes.GameSubmissionNeedsChanges);

        using var updateRequest = UserRequest(HttpMethod.Put, $"{ApiRoutes.V1}/game-submissions/{created.Game.Id}", submitter);
        updateRequest.Content = JsonContent.Create(Submission("Reviewed Game", needsChanges!.Game.Revision));
        using var updateResponse = await _client.SendAsync(updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();
        Assert.Equal("Reviewed Game", updated?.Game.Title);
        using var resubmitRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.V1}/game-submissions/{created.Game.Id}/submit", submitter);
        using var resubmitResponse = await _client.SendAsync(resubmitRequest);
        var resubmitted = await resubmitResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();

        using var approveRequest = AdminRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/submissions/{created.Game.Id}/approve", admin);
        approveRequest.Headers.Add("X-Correlation-ID", "moderation-approved");
        approveRequest.Content = JsonContent.Create(new ModerateSubmissionRequest(resubmitted!.Game.Revision));
        using var approveResponse = await _client.SendAsync(approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<GameSubmissionDto>();
        Assert.Equal("Approved", approved?.Game.ModerationStatus);
        Assert.NotNull(approved?.ApprovedAtUtc);
        Assert.Contains(await GetNotificationsAsync(submitter), item => item.Type == NotificationTypes.GameSubmissionApproved);

        using var visibleRequest = UserRequest(HttpMethod.Get, $"{ApiRoutes.V1}/games/{created.Game.Id}", other);
        using var visibleResponse = await _client.SendAsync(visibleRequest);
        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await dbContext.AuditLogs.Where(item => item.EntityId == created.Game.Id).OrderBy(item => item.TimestampUtc).ToListAsync();
        Assert.Collection(audit,
            item => { Assert.Equal("GameChangesRequested", item.Action); Assert.Equal("moderation-needs-changes", item.CorrelationId); },
            item => { Assert.Equal("GameApproved", item.Action); Assert.Equal("moderation-approved", item.CorrelationId); });
    }

    [Fact]
    public async Task ApprovedGameCorrectionRequiresAdminAndCurrentRevision()
    {
        var user = await CreateUserAsync("Correction User");
        var admin = await CreateUserAsync("Correction Admin", activateDevice: false);
        var gameId = await AddApprovedGameAsync();
        using var createRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.V1}/games/{gameId}/change-requests", user);
        createRequest.Content = JsonContent.Create(new CreateGameChangeRequestRequest(new GameChangePatchDto(MinimumAge: 10)));
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var change = await createResponse.Content.ReadFromJsonAsync<GameChangeRequestDto>();
        Assert.NotNull(change);

        using var forbiddenRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/change-requests/{change.Id}/approve", user);
        forbiddenRequest.Content = JsonContent.Create(new ReviewGameChangeRequestRequest(1));
        using var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using var staleRequest = AdminRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/change-requests/{change.Id}/approve", admin);
        staleRequest.Content = JsonContent.Create(new ReviewGameChangeRequestRequest(99));
        using var staleResponse = await _client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal(ModerationErrorCodes.RevisionConflict, await ErrorCodeAsync(staleResponse));

        using var approveRequest = AdminRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/change-requests/{change.Id}/approve", admin);
        approveRequest.Content = JsonContent.Create(new ReviewGameChangeRequestRequest(1, "Verified correction."));
        using var approveResponse = await _client.SendAsync(approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Contains(await GetNotificationsAsync(user), item => item.Type == NotificationTypes.SuggestedEditApproved);

        using var gameRequest = UserRequest(HttpMethod.Get, $"{ApiRoutes.V1}/games/{gameId}", user);
        using var gameResponse = await _client.SendAsync(gameRequest);
        var game = await gameResponse.Content.ReadFromJsonAsync<GameDto>();
        Assert.Equal(10, game?.MinimumAge);
        Assert.Equal(2, game?.Revision);
    }

    [Fact]
    public async Task ProposedImageStaysStagedUntilAdministratorApprovesIt()
    {
        var user = await CreateUserAsync("Image Correction User");
        var admin = await CreateUserAsync("Image Correction Admin");
        var gameId = await AddApprovedGameAsync();
        var oldImageId = Guid.NewGuid();
        var oldOriginalKey = $"games/{gameId:N}/front/{oldImageId:N}.jpg";
        var oldThumbnailKey = $"games/{gameId:N}/front/{oldImageId:N}.thumb.jpg";
        byte[] oldOriginal = [1, 2, 3];
        byte[] oldThumbnail = [4, 5, 6];
        await factory.ObjectStorage.WriteAsync(oldOriginalKey, oldOriginal, "image/jpeg");
        await factory.ObjectStorage.WriteAsync(oldThumbnailKey, oldThumbnail, "image/jpeg");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var image = GameImage.Create(oldImageId, gameId, GameImageType.Front, oldOriginalKey, "image/jpeg", 3, DateTime.UtcNow);
            image.MarkProcessing("image/jpeg", 3, 1, 1, new string('a', 64), DateTime.UtcNow);
            image.MarkReady(oldThumbnailKey, DateTime.UtcNow);
            await db.GameImages.AddAsync(image); await db.SaveChangesAsync();
        }

        using var createRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.V1}/games/{gameId}/change-requests", user);
        createRequest.Content = JsonContent.Create(new CreateGameChangeRequestRequest(new GameChangePatchDto(), true));
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var change = (await createResponse.Content.ReadFromJsonAsync<GameChangeRequestDto>())!;

        using var uploadRequest = UserRequest(HttpMethod.Put, $"{ApiRoutes.V1}/change-requests/{change.Id}/images/Front", user);
        uploadRequest.Content = new ByteArrayContent(OnePixelPng);
        uploadRequest.Content.Headers.ContentType = new("image/png");
        using var uploadResponse = await _client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var proposed = (await uploadResponse.Content.ReadFromJsonAsync<GameChangeRequestImageDto>())!;

        string stagedKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(oldImageId, (await db.GameImages.SingleAsync(item => item.GameId == gameId)).Id);
            stagedKey = (await db.GameChangeRequestImages.SingleAsync(item => item.Id == proposed.Id)).ObjectKey;
        }
        Assert.True(factory.ObjectStorage.Exists(stagedKey));
        Assert.True(factory.ObjectStorage.Exists(oldThumbnailKey));

        using var previewRequest = UserRequest(HttpMethod.Get, $"{ApiRoutes.V1}/change-request-images/{proposed.Id}/thumbnail", admin);
        previewRequest.Headers.Add(TestAuthenticationHandler.RolesHeader, "gamecollector-admin");
        using var previewResponse = await _client.SendAsync(previewRequest);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        using var approveRequest = AdminRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/change-requests/{change.Id}/approve", admin);
        approveRequest.Content = JsonContent.Create(new ReviewGameChangeRequestRequest(change.GameRevision));
        using var approveResponse = await _client.SendAsync(approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var live = await db.GameImages.SingleAsync(item => item.GameId == gameId && item.ImageType == GameImageType.Front);
            Assert.NotEqual(oldImageId, live.Id);
            Assert.Equal(stagedKey, live.ThumbnailObjectKey);
            Assert.Empty(await db.GameChangeRequestImages.Where(item => item.ChangeRequestId == change.Id).ToListAsync());
        }
        Assert.False(factory.ObjectStorage.Exists(oldOriginalKey));
        Assert.False(factory.ObjectStorage.Exists(oldThumbnailKey));
        Assert.True(factory.ObjectStorage.Exists(stagedKey));

        using var rejectedCreateRequest = UserRequest(HttpMethod.Post, $"{ApiRoutes.V1}/games/{gameId}/change-requests", user);
        rejectedCreateRequest.Content = JsonContent.Create(new CreateGameChangeRequestRequest(new GameChangePatchDto(), true));
        using var rejectedCreateResponse = await _client.SendAsync(rejectedCreateRequest);
        var rejectedChange = (await rejectedCreateResponse.Content.ReadFromJsonAsync<GameChangeRequestDto>())!;
        using var rejectedUploadRequest = UserRequest(HttpMethod.Put, $"{ApiRoutes.V1}/change-requests/{rejectedChange.Id}/images/Back", user);
        rejectedUploadRequest.Content = new ByteArrayContent(OnePixelPng);
        rejectedUploadRequest.Content.Headers.ContentType = new("image/png");
        using var rejectedUploadResponse = await _client.SendAsync(rejectedUploadRequest);
        var rejectedImage = (await rejectedUploadResponse.Content.ReadFromJsonAsync<GameChangeRequestImageDto>())!;
        string rejectedKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            rejectedKey = (await db.GameChangeRequestImages.SingleAsync(item => item.Id == rejectedImage.Id)).ObjectKey;
        }
        using var rejectRequest = AdminRequest(HttpMethod.Post, $"{ApiRoutes.AdminV1}/change-requests/{rejectedChange.Id}/reject", admin);
        rejectRequest.Content = JsonContent.Create(new ReviewGameChangeRequestRequest(rejectedChange.GameRevision, "The back image is unclear."));
        using var rejectResponse = await _client.SendAsync(rejectRequest);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        Assert.False(factory.ObjectStorage.Exists(rejectedKey));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.GameImages.AnyAsync(item => item.GameId == gameId && item.ImageType == GameImageType.Back));
        }
    }

    private async Task<List<NotificationDto>> GetNotificationsAsync(TestUser user)
    {
        using var request = UserRequest(HttpMethod.Get, ApiRoutes.V1 + "/me/notifications", user);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<NotificationDto>>())!;
    }

    private async Task AddReadyImagesAsync(Guid gameId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var type in new[] { GameImageType.Front, GameImageType.Back })
        {
            var image = GameImage.Create(Guid.NewGuid(), gameId, type, $"games/{gameId:N}/{type}/image.jpg", "image/jpeg", 100, DateTime.UtcNow);
            image.MarkProcessing("image/jpeg", 100, 100, 100, new string('a', 64), DateTime.UtcNow);
            image.MarkReady($"games/{gameId:N}/{type}/image.thumb.jpg", DateTime.UtcNow);
            await dbContext.GameImages.AddAsync(image);
        }
        _ = await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> AddApprovedGameAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var game = Game.Create(Guid.NewGuid(), "Correction Target", null, "Publisher", 2025, 2, 4, 8, 20, 30,
            ModerationStatus.Approved, null, DateTime.UtcNow);
        await dbContext.Games.AddAsync(game); _ = await dbContext.SaveChangesAsync(); return game.Id;
    }

    private async Task<TestUser> CreateUserAsync(string displayName, bool activateDevice = true)
    {
        var user = new TestUser("moderation-" + Guid.NewGuid().ToString("N"), Guid.NewGuid());
        using var onboard = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding");
        onboard.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        onboard.Content = JsonContent.Create(new OnboardUserRequest(displayName, "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard); Assert.Equal(HttpStatusCode.Created, onboardResponse.StatusCode);
        if (activateDevice)
        {
            using var activate = UserRequest(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
            activate.Content = JsonContent.Create(new ActivateDeviceRequest(user.DeviceId, "fcm-" + Guid.NewGuid().ToString("N")));
            using var response = await _client.SendAsync(activate); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        return user;
    }

    private static UpsertGameSubmissionRequest Submission(string title, long? revision = null) =>
        new(title, "Description", "Publisher", 2026, 2, 4, 8, 20, 30, [], [], [], revision);
    private static HttpRequestMessage UserRequest(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString()); return request;
    }
    private static HttpRequestMessage AdminRequest(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "gamecollector-admin"); return request;
    }
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
    private sealed record TestUser(string Subject, Guid DeviceId);
}
