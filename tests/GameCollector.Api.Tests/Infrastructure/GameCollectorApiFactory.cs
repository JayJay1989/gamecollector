using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GameCollector.Application.Abstractions.Media;
using GameCollector.Application.Abstractions.Notifications;

namespace GameCollector.Api.Tests.Infrastructure;

public sealed class GameCollectorApiFactory : WebApplicationFactory<Program>
{
    public TestObjectStorage ObjectStorage { get; } = new();
    public TestPushNotificationSender PushNotifications { get; } = new();
    private readonly string _databaseDirectory = Path.Combine(
        Path.GetTempPath(),
        "GameCollector.Api.Tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var databasePath = Path.Combine(_databaseDirectory, "gamecollector-tests.db");
        var connectionString = $"Data Source={databasePath};Foreign Keys=True;Default Timeout=5;Pooling=False";
        builder.UseSetting("ConnectionStrings:GameCollector", connectionString);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GameCollector"] = connectionString
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(ObjectStorage);
            services.RemoveAll<IPushNotificationSender>();
            services.AddSingleton<IPushNotificationSender>(PushNotifications);
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            services.AddControllers().AddApplicationPart(typeof(SecurityProbeController).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_databaseDirectory))
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
    }
}
