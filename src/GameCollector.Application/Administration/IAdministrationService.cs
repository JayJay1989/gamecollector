using GameCollector.Application.Common;
using GameCollector.Contracts.Admin;
using GameCollector.Contracts.Catalog;

namespace GameCollector.Application.Administration;

public interface IAdministrationService
{
    Task<Result<IReadOnlyList<AdminUserSummaryDto>>> SearchUsersAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<Result<AdminUserDetailDto>> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> SetUserDisabledAsync(Guid id, bool disabled, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeDeviceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AdminCollectionSummaryDto>>> SearchCollectionsAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<Result<AdminCollectionDetailDto>> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameDto>>> ListGamesAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> GetGameAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> CreateGameAsync(AdminGameRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> UpdateGameAsync(Guid id, AdminGameRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AdminAuditDto>>> SearchAuditAsync(string? action, string? entityType, Guid? entityId,
        Guid? actorUserId, DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SyncDiagnosticDto>>> GetSyncDiagnosticsAsync(Guid? userId, int limit, CancellationToken cancellationToken = default);
}
