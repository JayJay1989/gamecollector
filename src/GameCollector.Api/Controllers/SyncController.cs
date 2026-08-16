using GameCollector.Api.Authentication;
using GameCollector.Application.Sync;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/sync")]
public sealed class SyncController(ISyncService sync) : ControllerBase
{
    [HttpPost("push")]
    public async Task<IActionResult> Push(SyncPushRequest request, CancellationToken cancellationToken) => ToResponse(await sync.PushAsync(request, cancellationToken));
    [HttpPost("pull")]
    public async Task<IActionResult> Pull(SyncPullRequest request, CancellationToken cancellationToken) => ToResponse(await sync.PullAsync(request, cancellationToken));
    [HttpGet("bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken cancellationToken) => ToResponse(await sync.BootstrapAsync(cancellationToken));
    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
