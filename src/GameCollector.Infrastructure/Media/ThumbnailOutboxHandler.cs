using System.Text.Json;
using GameCollector.Application.Abstractions.Background;
using GameCollector.Application.Abstractions.Media;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Catalog;

namespace GameCollector.Infrastructure.Media;

public sealed class ThumbnailOutboxHandler(IGameImageRepository images, IObjectStorage storage,
    IImageProcessor processor, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IOutboxMessageHandler
{
    public string MessageType => OutboxMessageTypes.GenerateThumbnail;

    public async Task HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ThumbnailPayload>(payloadJson)
            ?? throw new InvalidDataException("The thumbnail payload is invalid.");
        var image = await images.GetByIdAsync(payload.MediaId, cancellationToken);
        if (image is null) return;
        if (image.Status == GameImageStatus.Ready)
        {
            await storage.DeleteAsync(image.OriginalObjectKey, cancellationToken);
            return;
        }
        if (image.Status != GameImageStatus.Processing) throw new InvalidDataException("The image is not ready for thumbnail processing.");
        var original = await storage.ReadAsync(image.OriginalObjectKey, GameCollector.Application.Media.MediaService.MaximumFileSizeBytes, cancellationToken);
        var thumbnail = processor.CreateThumbnail(original);
        var thumbnailKey = $"games/{image.GameId:N}/{image.ImageType.ToString().ToLowerInvariant()}/{image.Id:N}.thumb.jpg";
        await storage.WriteAsync(thumbnailKey, thumbnail, "image/jpeg", cancellationToken);
        image.MarkReady(thumbnailKey, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(image.OriginalObjectKey, cancellationToken);
    }

    private sealed record ThumbnailPayload(Guid MediaId);
}
