using GameCollector.Domain.Sync;

namespace GameCollector.Domain.Tests;

public sealed class SyncDiagnosticTests
{
    [Fact]
    public void SuccessfulSyncAccumulatesCountersAndClearsPriorError()
    {
        var now = DateTime.SpecifyKind(new DateTime(2026, 8, 15, 12, 0, 0), DateTimeKind.Utc);
        var item = SyncDiagnostic.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        item.RecordFailure("temporary failure", now);
        item.RecordSuccess(12, 3, 5, now.AddMinutes(1));
        item.RecordSuccess(10, 2, 4, now.AddMinutes(2));

        Assert.Equal(12, item.LastCursor);
        Assert.Equal(5, item.UploadedMutations);
        Assert.Equal(9, item.DownloadedEvents);
        Assert.Null(item.LastError);
        Assert.Null(item.LastErrorAtUtc);
    }
}
