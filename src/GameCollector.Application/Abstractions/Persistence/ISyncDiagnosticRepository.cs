using GameCollector.Domain.Sync;

namespace GameCollector.Application.Abstractions.Persistence;

public interface ISyncDiagnosticRepository
{
    Task<SyncDiagnostic?> GetAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncDiagnostic>> SearchAsync(Guid? userId, int limit, CancellationToken cancellationToken = default);
    Task AddAsync(SyncDiagnostic diagnostic, CancellationToken cancellationToken = default);
}
