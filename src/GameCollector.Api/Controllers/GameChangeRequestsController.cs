using GameCollector.Api.Authentication;
using GameCollector.Application.Moderation;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameCollector.Application.Media;

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

    [HttpPut("change-requests/{id:guid}/images/{imageType}")]
    [Consumes("image/jpeg", "image/png", "image/webp")]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<IActionResult> UploadImage(Guid id, string imageType, CancellationToken cancellationToken)
    {
        if (Request.ContentLength is null or < 1 or > MediaService.MaximumFileSizeBytes)
            return this.ToProblemResult(Application.Common.ApplicationErrors.InvalidMediaRequest);
        using var buffer = new MemoryStream((int)Request.ContentLength.Value);
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var result = await moderation.UploadChangeRequestImageAsync(id, imageType, Request.ContentType, buffer.ToArray(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpGet("change-request-images/{imageId:guid}/thumbnail")]
    public async Task<IActionResult> Thumbnail(Guid imageId, CancellationToken cancellationToken)
    {
        var result = await moderation.GetChangeRequestImageThumbnailAsync(imageId, cancellationToken);
        return result.IsSuccess ? File(result.Value!.Content, result.Value.ContentType) : this.ToProblemResult(result.Error!);
    }
}
