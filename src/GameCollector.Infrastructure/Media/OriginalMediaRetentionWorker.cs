using GameCollector.Application.Abstractions.Media;
using GameCollector.Application.Media;
using GameCollector.Domain.Catalog;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameCollector.Infrastructure.Media;

public sealed partial class OriginalMediaRetentionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OriginalMediaRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var processor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();
        var images = await database.GameImages
            .AsNoTracking()
            .Where(image => image.Status == GameImageStatus.Ready && image.ThumbnailObjectKey != null)
            .Select(image => new { image.Id, image.OriginalObjectKey, image.ThumbnailObjectKey })
            .ToListAsync(stoppingToken);

        foreach (var image in images)
        {
            if (string.Equals(image.OriginalObjectKey, image.ThumbnailObjectKey, StringComparison.Ordinal)) continue;
            try
            {
                var original = await storage.ReadAsync(
                    image.OriginalObjectKey,
                    MediaService.MaximumFileSizeBytes,
                    stoppingToken);
                var thumbnail = processor.CreateThumbnail(original);
                await storage.WriteAsync(image.ThumbnailObjectKey!, thumbnail, "image/jpeg", stoppingToken);
                await storage.DeleteAsync(image.OriginalObjectKey, stoppingToken);
                LogRetentionApplied(logger, image.Id);
            }
            catch (ObjectNotFoundException)
            {
                // The original was already removed by a previous run.
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRetentionFailure(logger, image.Id, exception);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Information,
        "Regenerated thumbnail and removed retained original for media {MediaId}.")]
    private static partial void LogRetentionApplied(ILogger logger, Guid mediaId);

    [LoggerMessage(2, LogLevel.Warning,
        "Could not apply thumbnail-only retention to media {MediaId}.")]
    private static partial void LogRetentionFailure(ILogger logger, Guid mediaId, Exception exception);
}
