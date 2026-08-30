using GameCollector.Infrastructure.Persistence;
using GameCollector.Infrastructure.Persistence.Repositories;
using GameCollector.Application.Abstractions.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using GameCollector.Application.Abstractions.ExternalCatalog;
using GameCollector.Application.Abstractions.Media;
using GameCollector.Infrastructure.ExternalCatalog;
using GameCollector.Infrastructure.Media;
using Minio;
using GameCollector.Application.Abstractions.Background;
using GameCollector.Infrastructure.Background;
using GameCollector.Application.Abstractions.Notifications;
using GameCollector.Infrastructure.Notifications;

namespace GameCollector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GameCollector")
            ?? throw new InvalidOperationException(
                "Connection string 'GameCollector' is required.");

        _ = new SqliteConnectionStringBuilder(connectionString);

        services.AddSingleton<SqliteConnectionPragmaInterceptor>();
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options
                .UseSqlite(connectionString, sqliteOptions =>
                    sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .AddInterceptors(serviceProvider.GetRequiredService<SqliteConnectionPragmaInterceptor>()));
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IDeviceRegistrationRepository, DeviceRegistrationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<ICollectionInvitationRepository, CollectionInvitationRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICollectionGameRepository, CollectionGameRepository>();
        services.AddScoped<IWishlistRepository, WishlistRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IGameImageRepository, GameImageRepository>();
        services.AddScoped<IGameChangeRequestRepository, GameChangeRequestRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ISyncRepository, SyncRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISyncDiagnosticRepository, SyncDiagnosticRepository>();
        services.AddScoped<OutboxRepository>();
        services.AddScoped<IOutboxRepository>(provider => provider.GetRequiredService<OutboxRepository>());
        services.AddScoped<IOutboxWriter>(provider => provider.GetRequiredService<OutboxRepository>());
        services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
        services.AddScoped<IOutboxMessageHandler, ThumbnailOutboxHandler>();
        services.AddScoped<IOutboxMessageHandler, NotificationOutboxHandler>();
        services.AddHostedService<OutboxProcessor>();

        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
        var firebaseOptions = configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new FirebaseOptions();
        services.AddSingleton<DisabledPushNotificationSender>();
        services.AddHttpClient<FirebasePushNotificationSender>(client =>
        {
            client.BaseAddress = new Uri("https://fcm.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IPushNotificationSender>(provider => string.IsNullOrWhiteSpace(firebaseOptions.ProjectId)
            ? provider.GetRequiredService<DisabledPushNotificationSender>()
            : provider.GetRequiredService<FirebasePushNotificationSender>());

        services.Configure<MediaStorageOptions>(configuration.GetSection(MediaStorageOptions.SectionName));
        var mediaOptions = configuration.GetSection(MediaStorageOptions.SectionName).Get<MediaStorageOptions>() ?? new MediaStorageOptions();
        services.AddSingleton<IMinioClient>(_ =>
        {
            var client = new MinioClient()
                .WithEndpoint(mediaOptions.Endpoint)
                .WithCredentials(mediaOptions.AccessKey, mediaOptions.SecretKey);
            if (mediaOptions.UseSsl) client = client.WithSSL();
            return client.Build();
        });
        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddHostedService<OriginalMediaRetentionWorker>();

        services.Configure<ExternalCatalogOptions>(configuration.GetSection(ExternalCatalogOptions.SectionName));
        var externalOptions = configuration.GetSection(ExternalCatalogOptions.SectionName).Get<ExternalCatalogOptions>() ?? new ExternalCatalogOptions();
        services.AddMemoryCache();
        services.AddHttpClient<UpcItemDbProductMetadataProvider>(client =>
        {
            client.BaseAddress = new Uri(externalOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(externalOptions.TimeoutSeconds, 1, 30));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddScoped<IProductMetadataProvider, CachedProductMetadataProvider>();
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                "sqlite",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
