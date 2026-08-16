using GameCollector.Api.Authentication;
using GameCollector.Application.Collections;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
public sealed class InvitationsController(ICollectionService service) : ControllerBase
{
    [HttpGet(ApiRoutes.V1 + "/me/invitations")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var result = await service.GetMyInvitationsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpPost(ApiRoutes.V1 + "/invitations/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken) => ToResponse(await service.RespondToInvitationAsync(id, true, cancellationToken));

    [HttpPost(ApiRoutes.V1 + "/invitations/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken) => ToResponse(await service.RespondToInvitationAsync(id, false, cancellationToken));

    private IActionResult ToResponse(Application.Common.Result<bool> result) => result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
}
