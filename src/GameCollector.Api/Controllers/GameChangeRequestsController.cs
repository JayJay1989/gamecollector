using GameCollector.Api.Authentication;
using GameCollector.Application.Moderation;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1)]
public sealed class GameChangeRequestsController(IModerationService moderation) : ControllerBase
{
    [HttpPost("games/{gameId:guid}/change-requests")]
    public async Task<IActionResult> Create(Guid gameId, CreateGameChangeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await moderation.CreateChangeRequestAsync(gameId, request, cancellationToken);
        return result.IsSuccess ? Created($"{ApiRoutes.V1}/change-requests/mine", result.Value) : this.ToProblemResult(result.Error!);
    }
    [HttpGet("change-requests/mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var result = await moderation.GetMyChangeRequestsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }
}
