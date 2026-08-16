using GameCollector.Api.Authentication;
using GameCollector.Application.Administration;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[Route(ApiRoutes.AdminV1 + "/collections")]
public sealed class AdminCollectionsController(IAdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) => ToResponse(await administration.SearchCollectionsAsync(q, limit, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await administration.GetCollectionAsync(id, cancellationToken));

    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
