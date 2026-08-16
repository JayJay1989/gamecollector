using GameCollector.Api.Authentication;
using GameCollector.Application.Moderation;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[Route(ApiRoutes.AdminV1)]
public sealed class AdminModerationController(IModerationService moderation) : ControllerBase
{
    [HttpGet("submissions")]
    public async Task<IActionResult> Submissions([FromQuery] string? status, CancellationToken cancellationToken) => ToResponse(await moderation.GetModerationQueueAsync(status, cancellationToken));
    [HttpGet("submissions/{id:guid}")]
    public async Task<IActionResult> Submission(Guid id, CancellationToken cancellationToken) => ToResponse(await moderation.GetSubmissionForModerationAsync(id, cancellationToken));
    [HttpPost("submissions/{id:guid}/approve")]
    public async Task<IActionResult> ApproveSubmission(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken) => ToResponse(await moderation.ApproveSubmissionAsync(id, request, cancellationToken));
    [HttpPost("submissions/{id:guid}/needs-changes")]
    public async Task<IActionResult> NeedsChanges(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken) => ToResponse(await moderation.RequestSubmissionChangesAsync(id, request, cancellationToken));
    [HttpPost("submissions/{id:guid}/reject")]
    public async Task<IActionResult> RejectSubmission(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken) => ToResponse(await moderation.RejectSubmissionAsync(id, request, cancellationToken));
    [HttpGet("change-requests")]
    public async Task<IActionResult> ChangeRequests([FromQuery] string? status, CancellationToken cancellationToken) => ToResponse(await moderation.GetChangeRequestQueueAsync(status, cancellationToken));
    [HttpPost("change-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveChangeRequest(Guid id, ReviewGameChangeRequestRequest request, CancellationToken cancellationToken) => ToResponse(await moderation.ApproveChangeRequestAsync(id, request, cancellationToken));
    [HttpPost("change-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectChangeRequest(Guid id, ReviewGameChangeRequestRequest request, CancellationToken cancellationToken) => ToResponse(await moderation.RejectChangeRequestAsync(id, request, cancellationToken));
    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
