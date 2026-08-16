using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class SyncRepository(ApplicationDbContext dbContext) : ISyncRepository
{
    public Task<ProcessedMutation?> GetProcessedMutationAsync(Guid userId, Guid mutationId, CancellationToken cancellationToken = default) =>
        dbContext.ProcessedMutations.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.MutationId == mutationId, cancellationToken);
    public async Task AddProcessedMutationAsync(ProcessedMutation mutation, CancellationToken cancellationToken = default) => await dbContext.ProcessedMutations.AddAsync(mutation, cancellationToken);
    public async Task AddEventAsync(SyncEvent syncEvent, CancellationToken cancellationToken = default) => await dbContext.SyncEvents.AddAsync(syncEvent, cancellationToken);
    public async Task<IReadOnlyList<SyncEvent>> GetEventsAsync(string scopeType, Guid? scopeId, long afterSequence, int limit, CancellationToken cancellationToken = default) =>
        await dbContext.SyncEvents.AsNoTracking().Where(item => item.ScopeType == scopeType && item.ScopeId == scopeId && item.Sequence > afterSequence)
            .OrderBy(item => item.Sequence).Take(limit).ToListAsync(cancellationToken);
    public async Task<long> GetMaximumSequenceAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SyncEvents.MaxAsync(item => (long?)item.Sequence, cancellationToken) ?? 0;
    public async Task<long> GetMinimumCursorAsync(string scopeType, Guid? scopeId, CancellationToken cancellationToken = default) =>
        await dbContext.SyncRetentionStates.Where(item => item.ScopeKey == ScopeKey(scopeType, scopeId))
            .Select(item => (long?)item.MinimumCursor).SingleOrDefaultAsync(cancellationToken) ?? 0;
    private static string ScopeKey(string scopeType, Guid? scopeId) => scopeId.HasValue ? $"{scopeType}:{scopeId.Value:N}" : scopeType;
}
