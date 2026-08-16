using GameCollector.Api.Authentication;
using GameCollector.Application.Administration;
using GameCollector.Contracts.Admin;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[Route(ApiRoutes.AdminV1 + "/games")]
public sealed class AdminCatalogController(IAdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) => ToResponse(await administration.ListGamesAsync(q, limit, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => ToResponse(await administration.GetGameAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(AdminGameRequest request, CancellationToken cancellationToken)
    {
        var result = await administration.CreateGameAsync(request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, AdminGameRequest request, CancellationToken cancellationToken) =>
        ToResponse(await administration.UpdateGameAsync(id, request, cancellationToken));

    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
