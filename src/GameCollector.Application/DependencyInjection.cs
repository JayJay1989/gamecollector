using GameCollector.Application.Users;
using GameCollector.Application.Collections;
using GameCollector.Application.Catalog;
using GameCollector.Application.ExternalCatalog;
using GameCollector.Application.Media;
using GameCollector.Application.Moderation;
using GameCollector.Application.Sync;
using GameCollector.Application.Notifications;
using GameCollector.Application.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameCollector.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IOwnershipService, OwnershipService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IExternalProductLookupService, ExternalProductLookupService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<ISyncEventWriter, SyncEventWriter>();
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(provider => provider.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationWriter>(provider => provider.GetRequiredService<NotificationService>());
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
