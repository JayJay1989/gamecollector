using GameCollector.Api.Authentication;
using GameCollector.Application.Moderation;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/game-submissions")]
public sealed class GameSubmissionsController(IModerationService moderation) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(UpsertGameSubmissionRequest request, CancellationToken cancellationToken)
    {
        var result = await moderation.CreateSubmissionAsync(request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Game.Id }, result.Value) : this.ToProblemResult(result.Error!);
    }
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) => ToResponse(await moderation.GetMySubmissionsAsync(cancellationToken));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => ToResponse(await moderation.GetMySubmissionAsync(id, cancellationToken));
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpsertGameSubmissionRequest request, CancellationToken cancellationToken) => ToResponse(await moderation.UpdateSubmissionAsync(id, request, cancellationToken));
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await moderation.DeleteSubmissionAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
    }
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken) => ToResponse(await moderation.SubmitAsync(id, cancellationToken));
    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
