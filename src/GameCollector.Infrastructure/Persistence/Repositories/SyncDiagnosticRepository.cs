using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class SyncDiagnosticRepository(ApplicationDbContext dbContext) : ISyncDiagnosticRepository
{
    public Task<SyncDiagnostic?> GetAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default) =>
        dbContext.SyncDiagnostics.SingleOrDefaultAsync(item => item.UserId == userId && item.DeviceId == deviceId, cancellationToken);

    public async Task<IReadOnlyList<SyncDiagnostic>> SearchAsync(Guid? userId, int limit, CancellationToken cancellationToken = default)
    {
        var source = dbContext.SyncDiagnostics.AsNoTracking().AsQueryable();
        if (userId.HasValue) source = source.Where(item => item.UserId == userId.Value);
        return await source.OrderByDescending(item => item.LastSuccessfulSyncAtUtc).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SyncDiagnostic diagnostic, CancellationToken cancellationToken = default) =>
        await dbContext.SyncDiagnostics.AddAsync(diagnostic, cancellationToken);
}
