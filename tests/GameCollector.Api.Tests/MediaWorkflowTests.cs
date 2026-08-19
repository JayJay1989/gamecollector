using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Media;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Catalog;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class MediaWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OwnerCanUploadValidatedImageThroughApiAndThumbnailBecomesReady()
    {
        var context = await CreatePendingGameAsync();
        using var intentRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/media/upload-intents", context);
        intentRequest.Content = JsonContent.Create(new CreateUploadIntentRequest(
            context.GameId, "Front", "image/png", OnePixelPng.LongLength));
        using var intentResponse = await _client.SendAsync(intentRequest);
        Assert.Equal(HttpStatusCode.Created, intentResponse.StatusCode);
        var intent = await intentResponse.Content.ReadFromJsonAsync<UploadIntentDto>();
        Assert.NotNull(intent);
        Assert.True(intent.ExpiresAtUtc > DateTime.UtcNow);

        using var uploadRequest = Request(HttpMethod.Put, $"{ApiRoutes.V1}/media/{intent.MediaId}/content", context);
        uploadRequest.Content = new ByteArrayContent(OnePixelPng);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var uploadResponse = await _client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);

        GameImageDto? image = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var getRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/media/{intent.MediaId}", context);
            using var getResponse = await _client.SendAsync(getRequest);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            image = await getResponse.Content.ReadFromJsonAsync<GameImageDto>();
            if (image?.Status == "Ready") break;
            await Task.Delay(25);
        }

        Assert.NotNull(image);
        Assert.Equal("Ready", image.Status);
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Null(image.OriginalUrl);
        Assert.NotNull(image.ThumbnailUrl);
        Assert.Equal(64, image.Checksum?.Length);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await using var waitScope = factory.Services.CreateAsyncScope();
            var waitDatabase = waitScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mediaId = intent.MediaId.ToString();
            if (await waitDatabase.OutboxMessages.AnyAsync(message =>
                    message.PayloadJson.Contains(mediaId) && message.ProcessedAtUtc != null)) break;
            await Task.Delay(25);
        }

        var uploadKey = Uri.UnescapeDataString(intent.UploadUrl.AbsolutePath["/upload/".Length..]);
        Assert.False(factory.ObjectStorage.Exists(uploadKey));

        using var listRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/media/games/{context.GameId}", context);
        using var listResponse = await _client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listed = await listResponse.Content.ReadFromJsonAsync<List<GameImageDto>>();
        Assert.Equal(intent.MediaId, Assert.Single(listed!).Id);

        using var thumbnailRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/media/{intent.MediaId}/thumbnail", context);
        using var thumbnailResponse = await _client.SendAsync(thumbnailRequest);
        Assert.Equal(HttpStatusCode.OK, thumbnailResponse.StatusCode);
        Assert.Equal("image/jpeg", thumbnailResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await thumbnailResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task CompletionRejectsActualFormatThatDoesNotMatchDeclaredMimeType()
    {
        var context = await CreatePendingGameAsync();
        var bytes = OnePixelPng;
        using var intentRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/media/upload-intents", context);
        intentRequest.Content = JsonContent.Create(new CreateUploadIntentRequest(context.GameId, "Back", "image/jpeg", bytes.LongLength));
        using var intentResponse = await _client.SendAsync(intentRequest);
        var intent = await intentResponse.Content.ReadFromJsonAsync<UploadIntentDto>();
        Assert.NotNull(intent);
        factory.ObjectStorage.Upload(intent.UploadUrl, bytes);

        using var completeRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/media/{intent.MediaId}/complete", context);
        using var response = await _client.SendAsync(completeRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(MediaErrorCodes.InvalidImage, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DurableThumbnailWorkRetriesAfterTransientStorageFailure()
    {
        var context = await CreatePendingGameAsync();
        using var intentRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/media/upload-intents", context);
        intentRequest.Content = JsonContent.Create(new CreateUploadIntentRequest(context.GameId, "Front", "image/png", OnePixelPng.LongLength));
        using var intentResponse = await _client.SendAsync(intentRequest);
        var intent = await intentResponse.Content.ReadFromJsonAsync<UploadIntentDto>();
        Assert.NotNull(intent);
        factory.ObjectStorage.Upload(intent.UploadUrl, OnePixelPng);
        factory.ObjectStorage.WritesToFail = 1;
        try
        {
            using var completeRequest = Request(HttpMethod.Post, $"{ApiRoutes.V1}/media/{intent.MediaId}/complete", context);
            using var completeResponse = await _client.SendAsync(completeRequest);
            Assert.Equal(HttpStatusCode.Accepted, completeResponse.StatusCode);

            for (var attempt = 0; attempt < 50; attempt++)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var mediaId = intent.MediaId.ToString();
                var message = await dbContext.OutboxMessages.SingleAsync(item => item.PayloadJson.Contains(mediaId));
                if (message.ProcessedAtUtc is not null)
                {
                    Assert.Equal(1, message.Attempts);
                    return;
                }
                await Task.Delay(100);
            }

            Assert.Fail("The durable thumbnail message was not retried.");
        }
        finally
        {
            factory.ObjectStorage.WritesToFail = 0;
        }
    }

    private async Task<TestContext> CreatePendingGameAsync()
    {
        var subject = "media-" + Guid.NewGuid().ToString("N");
        var deviceId = Guid.NewGuid();
        using var onboard = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding");
        onboard.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
        onboard.Content = JsonContent.Create(new OnboardUserRequest("Media User", "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard);
        Assert.Equal(HttpStatusCode.Created, onboardResponse.StatusCode);
        using var activate = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate");
        activate.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
        activate.Headers.Add(DeviceHeaders.DeviceId, deviceId.ToString());
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(deviceId, "fcm-" + Guid.NewGuid().ToString("N")));
        using var activateResponse = await _client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var gameId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await dbContext.UserProfiles.SingleAsync(user => user.IdentitySubject == subject);
        await dbContext.Games.AddAsync(Game.Create(gameId, "Media Test Game", null, null, null, null, null,
            null, null, null, ModerationStatus.Draft, profile.Id, DateTime.UtcNow));
        _ = await dbContext.SaveChangesAsync();
        return new TestContext(subject, deviceId, gameId);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestContext context)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, context.Subject);
        request.Headers.Add(DeviceHeaders.DeviceId, context.DeviceId.ToString());
        return request;
    }

    private sealed record TestContext(string Subject, Guid DeviceId, Guid GameId);
}
