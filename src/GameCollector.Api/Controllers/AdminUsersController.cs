using GameCollector.Api.Authentication;
using GameCollector.Application.Administration;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[Route(ApiRoutes.AdminV1 + "/users")]
public sealed class AdminUsersController(IAdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) => ToResponse(await administration.SearchUsersAsync(q, limit, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await administration.GetUserAsync(id, cancellationToken));

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken) =>
        ToNoContent(await administration.SetUserDisabledAsync(id, true, cancellationToken));

    [HttpPost("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken) =>
        ToNoContent(await administration.SetUserDisabledAsync(id, false, cancellationToken));

    [HttpPost("{id:guid}/revoke-device")]
    public async Task<IActionResult> RevokeDevice(Guid id, CancellationToken cancellationToken) =>
        ToNoContent(await administration.RevokeDeviceAsync(id, cancellationToken));

    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    private IActionResult ToNoContent(Application.Common.Result<bool> result) => result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
}
