using GameCollector.Domain.Sync;

namespace GameCollector.Application.Abstractions.Persistence;

public interface ISyncRepository
{
    Task<ProcessedMutation?> GetProcessedMutationAsync(Guid userId, Guid mutationId, CancellationToken cancellationToken = default);
    Task AddProcessedMutationAsync(ProcessedMutation mutation, CancellationToken cancellationToken = default);
    Task AddEventAsync(SyncEvent syncEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncEvent>> GetEventsAsync(string scopeType, Guid? scopeId, long afterSequence, int limit, CancellationToken cancellationToken = default);
    Task<long> GetMaximumSequenceAsync(CancellationToken cancellationToken = default);
    Task<long> GetMinimumCursorAsync(string scopeType, Guid? scopeId, CancellationToken cancellationToken = default);
}
