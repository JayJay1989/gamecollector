using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Users;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class ProfileAndDeviceTests(GameCollectorApiFactory factory)
    : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MissingProfileReturnsOnboardingSignal()
    {
        using var request = CreateRequest(HttpMethod.Get, ApiRoutes.V1 + "/me", NewSubject());

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(UserErrorCodes.ProfileNotFound, await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task OnboardingCreatesProfileFromTokenSubject()
    {
        var subject = NewSubject();
        var username = NewUsername();

        using var response = await OnboardAsync(subject, "John Smith", username);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal("John Smith", profile.DisplayName);
        Assert.Equal(username, profile.Username);
        Assert.False(profile.HasActiveDevice);

        using var getRequest = CreateRequest(HttpMethod.Get, ApiRoutes.V1 + "/me", subject);
        using var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task UsernamesAreGloballyCaseInsensitive()
    {
        var username = NewUsername();
        using var firstResponse = await OnboardAsync(NewSubject(), "First User", username);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await OnboardAsync(
            NewSubject(),
            "Second User",
            username.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(UserErrorCodes.UsernameAlreadyExists, await ReadErrorCodeAsync(secondResponse));
    }

    [Fact]
    public async Task IdentitySubjectCanOnlyOnboardOnce()
    {
        var subject = NewSubject();
        using var firstResponse = await OnboardAsync(subject, "First Profile", NewUsername());
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await OnboardAsync(subject, "Second Profile", NewUsername());

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(UserErrorCodes.ProfileAlreadyExists, await ReadErrorCodeAsync(secondResponse));
    }

    [Fact]
    public async Task NewDeviceReplacesOldDevice()
    {
        var subject = NewSubject();
        using var onboardingResponse = await OnboardAsync(subject, "Device User", NewUsername());
        Assert.Equal(HttpStatusCode.Created, onboardingResponse.StatusCode);
        var firstDevice = Guid.NewGuid();
        var secondDevice = Guid.NewGuid();

        using var firstActivation = await ActivateDeviceAsync(subject, firstDevice, "first-fcm-token");
        Assert.Equal(HttpStatusCode.OK, firstActivation.StatusCode);

        using var firstUpdate = await UpdateProfileAsync(subject, firstDevice, "First Name");
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        using var secondActivation = await ActivateDeviceAsync(subject, secondDevice, "second-fcm-token");
        Assert.Equal(HttpStatusCode.OK, secondActivation.StatusCode);

        using var oldDeviceUpdate = await UpdateProfileAsync(subject, firstDevice, "Old Device");
        Assert.Equal(HttpStatusCode.Forbidden, oldDeviceUpdate.StatusCode);
        Assert.Equal(UserErrorCodes.DeviceNotActive, await ReadErrorCodeAsync(oldDeviceUpdate));

        using var newDeviceUpdate = await UpdateProfileAsync(subject, secondDevice, "New Device");
        Assert.Equal(HttpStatusCode.OK, newDeviceUpdate.StatusCode);
    }

    [Fact]
    public async Task RevokedDeviceCannotUseProtectedEndpoint()
    {
        var subject = NewSubject();
        var deviceId = Guid.NewGuid();
        using var onboardingResponse = await OnboardAsync(subject, "Revoke User", NewUsername());
        Assert.Equal(HttpStatusCode.Created, onboardingResponse.StatusCode);
        using var activationResponse = await ActivateDeviceAsync(subject, deviceId, "revoke-fcm-token");
        Assert.Equal(HttpStatusCode.OK, activationResponse.StatusCode);

        using var revokeRequest = CreateRequest(
            HttpMethod.Delete,
            ApiRoutes.V1 + "/me/device",
            subject,
            deviceId);
        using var revokeResponse = await _client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var updateResponse = await UpdateProfileAsync(subject, deviceId, "After Revoke");
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(UserErrorCodes.DeviceNotActive, await ReadErrorCodeAsync(updateResponse));
    }

    [Fact]
    public async Task EmptyProfilePatchReturnsDomainValidationError()
    {
        var subject = NewSubject();
        var deviceId = Guid.NewGuid();
        using var onboardingResponse = await OnboardAsync(subject, "Validation User", NewUsername());
        Assert.Equal(HttpStatusCode.Created, onboardingResponse.StatusCode);
        using var activationResponse = await ActivateDeviceAsync(subject, deviceId, "validation-fcm-token");
        Assert.Equal(HttpStatusCode.OK, activationResponse.StatusCode);
        using var request = CreateRequest(HttpMethod.Patch, ApiRoutes.V1 + "/me", subject, deviceId);
        request.Content = JsonContent.Create(new UpdateUserProfileRequest(null, null));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ApiErrorCodes.DomainValidationFailed, await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task DisabledProfileIsRejectedByApplication()
    {
        var subject = NewSubject();
        using var onboardingResponse = await OnboardAsync(subject, "Disabled User", NewUsername());
        Assert.Equal(HttpStatusCode.Created, onboardingResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await dbContext.UserProfiles.SingleAsync(
                user => user.IdentitySubject == subject);
            profile.Disable(DateTime.UtcNow);
            _ = await dbContext.SaveChangesAsync();
        }

        using var getRequest = CreateRequest(HttpMethod.Get, ApiRoutes.V1 + "/me", subject);
        using var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(UserErrorCodes.UserDisabled, await ReadErrorCodeAsync(getResponse));
    }

    private async Task<HttpResponseMessage> OnboardAsync(
        string subject,
        string displayName,
        string username)
    {
        var request = CreateRequest(
            HttpMethod.Post,
            ApiRoutes.V1 + "/me/onboarding",
            subject);
        request.Content = JsonContent.Create(new OnboardUserRequest(displayName, username));
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> ActivateDeviceAsync(
        string subject,
        Guid deviceId,
        string fcmToken)
    {
        var request = CreateRequest(
            HttpMethod.Post,
            ApiRoutes.V1 + "/me/device/activate",
            subject);
        request.Content = JsonContent.Create(new ActivateDeviceRequest(deviceId, fcmToken));
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> UpdateProfileAsync(
        string subject,
        Guid deviceId,
        string displayName)
    {
        var request = CreateRequest(
            HttpMethod.Patch,
            ApiRoutes.V1 + "/me",
            subject,
            deviceId);
        request.Content = JsonContent.Create(new UpdateUserProfileRequest(displayName, null));
        return await _client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string subject,
        Guid? deviceId = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
        if (deviceId.HasValue)
        {
            request.Headers.Add(DeviceHeaders.DeviceId, deviceId.Value.ToString());
        }

        return request;
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private static string NewSubject() => "subject-" + Guid.NewGuid().ToString("N");

    private static string NewUsername() => "u" + Guid.NewGuid().ToString("N")[..12];
}
