using System.Net.Http.Headers;
using System.Net.Http.Json;
using GameCollector.Application.Abstractions.Notifications;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace GameCollector.Infrastructure.Notifications;

public sealed class FirebasePushNotificationSender(HttpClient client, IOptions<FirebaseOptions> options)
    : IPushNotificationSender
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly FirebaseOptions _options = options.Value;

    public async Task<PushSendResult> SendAsync(string fcmToken, Guid notificationId, string type,
        CancellationToken cancellationToken = default)
    {
        var credential = string.IsNullOrWhiteSpace(_options.CredentialsPath)
            ? await GoogleCredential.GetApplicationDefaultAsync(cancellationToken)
            : (await CredentialFactory.FromFileAsync<ServiceAccountCredential>(_options.CredentialsPath, cancellationToken)).ToGoogleCredential();
        credential = credential.CreateScoped(MessagingScope);
        var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                token = fcmToken,
                data = new Dictionary<string, string>
                {
                    ["notificationId"] = notificationId.ToString(),
                    ["type"] = type
                },
                android = new { priority = "high" }
            }
        });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            if (detail.Length > 1000) detail = detail[..1000];
            throw new HttpRequestException($"FCM returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
        }
        return PushSendResult.Sent;
    }
}

public sealed class DisabledPushNotificationSender : IPushNotificationSender
{
    public Task<PushSendResult> SendAsync(string fcmToken, Guid notificationId, string type,
        CancellationToken cancellationToken = default) => Task.FromResult(PushSendResult.Disabled);
}
