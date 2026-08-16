using GameCollector.Application.Common;
using GameCollector.Contracts.Sync;

namespace GameCollector.Application.Sync;

public interface ISyncService
{
    Task<Result<SyncPushResponse>> PushAsync(SyncPushRequest request, CancellationToken cancellationToken = default);
    Task<Result<SyncPullResponse>> PullAsync(SyncPullRequest request, CancellationToken cancellationToken = default);
    Task<Result<SyncBootstrapDto>> BootstrapAsync(CancellationToken cancellationToken = default);
}
