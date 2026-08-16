using GameCollector.Api.Authentication;
using GameCollector.Application.Administration;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[Route(ApiRoutes.AdminV1)]
public sealed class AdminOperationsController(IAdministrationService administration) : ControllerBase
{
    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? action, [FromQuery] string? entityType,
        [FromQuery] Guid? entityId, [FromQuery] Guid? actorUserId, [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc, [FromQuery] int limit = 100, CancellationToken cancellationToken = default) =>
        ToResponse(await administration.SearchAuditAsync(action, entityType, entityId, actorUserId,
            fromUtc, toUtc, limit, cancellationToken));

    [HttpGet("diagnostics/sync")]
    public async Task<IActionResult> SyncDiagnostics([FromQuery] Guid? userId, [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default) =>
        ToResponse(await administration.GetSyncDiagnosticsAsync(userId, limit, cancellationToken));

    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
