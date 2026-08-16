using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Domain.Tests;

public sealed class GameImageTests
{
    [Fact]
    public void ImageMovesThroughValidatedProcessingLifecycle()
    {
        var created = DateTime.UtcNow;
        var image = GameImage.Create(Guid.NewGuid(), Guid.NewGuid(), GameImageType.Front,
            "games/game/front/image.jpg", "image/jpeg", 100, created);
        image.MarkProcessing("image/jpeg", 100, 1200, 800, new string('a', 64), created.AddSeconds(1));
        image.MarkReady("games/game/front/image.thumb.jpg", created.AddSeconds(2));

        Assert.Equal(GameImageStatus.Ready, image.Status);
        Assert.Equal(1200, image.Width);
        Assert.Equal("games/game/front/image.thumb.jpg", image.ThumbnailObjectKey);
    }

    [Fact]
    public void ReadyImageCannotBeMarkedFailed()
    {
        var image = GameImage.Create(Guid.NewGuid(), Guid.NewGuid(), GameImageType.Back,
            "games/game/back/image.png", "image/png", 50, DateTime.UtcNow);
        image.MarkProcessing("image/png", 50, 10, 10, new string('b', 64), DateTime.UtcNow);
        image.MarkReady("games/game/back/image.thumb.jpg", DateTime.UtcNow);

        Assert.Throws<DomainValidationException>(() => image.MarkFailed(DateTime.UtcNow));
    }

    [Fact]
    public void ReadyImageCanRestartAsAReplacementUpload()
    {
        var image = GameImage.Create(Guid.NewGuid(), Guid.NewGuid(), GameImageType.Back,
            "games/game/back/old.jpg", "image/jpeg", 50, DateTime.UtcNow);
        image.MarkProcessing("image/jpeg", 50, 10, 10, new string('c', 64), DateTime.UtcNow);
        image.MarkReady("games/game/back/old.thumb.jpg", DateTime.UtcNow);

        image.RestartUpload("games/game/back/new.png", "image/png", 75, DateTime.UtcNow);

        Assert.Equal(GameImageStatus.PendingUpload, image.Status);
        Assert.Equal("games/game/back/new.png", image.OriginalObjectKey);
        Assert.Null(image.ThumbnailObjectKey);
        Assert.Null(image.Checksum);
    }
}
