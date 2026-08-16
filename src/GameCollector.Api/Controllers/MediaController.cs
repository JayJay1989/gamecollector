using GameCollector.Api.Authentication;
using GameCollector.Application.Media;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/media")]
public sealed class MediaController(IMediaService media) : ControllerBase
{
    [HttpPost("upload-intents")]
    public async Task<IActionResult> CreateUploadIntent(CreateUploadIntentRequest request, CancellationToken cancellationToken)
    {
        var result = await media.CreateUploadIntentAsync(request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.MediaId }, result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await media.CompleteAsync(id, cancellationToken);
        return result.IsSuccess ? AcceptedAtAction(nameof(Get), new { id }, result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await media.GetAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }
}
