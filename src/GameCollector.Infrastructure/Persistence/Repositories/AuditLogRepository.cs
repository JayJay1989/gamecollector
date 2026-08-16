using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(ApplicationDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entity, CancellationToken cancellationToken = default) => await dbContext.AuditLogs.AddAsync(entity, cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(string? action, string? entityType, Guid? entityId,
        Guid? actorUserId, DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken cancellationToken = default)
    {
        var source = dbContext.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) source = source.Where(item => item.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(entityType)) source = source.Where(item => item.EntityType == entityType.Trim());
        if (entityId.HasValue) source = source.Where(item => item.EntityId == entityId.Value);
        if (actorUserId.HasValue) source = source.Where(item => item.ActorUserId == actorUserId.Value);
        if (fromUtc.HasValue) source = source.Where(item => item.TimestampUtc >= fromUtc.Value);
        if (toUtc.HasValue) source = source.Where(item => item.TimestampUtc <= toUtc.Value);
        return await source.OrderByDescending(item => item.TimestampUtc).Take(limit).ToListAsync(cancellationToken);
    }
}
