using GameCollector.Domain.Auditing;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> SearchAsync(string? action, string? entityType, Guid? entityId,
        Guid? actorUserId, DateTime? fromUtc, DateTime? toUtc, int limit,
        CancellationToken cancellationToken = default);
}
