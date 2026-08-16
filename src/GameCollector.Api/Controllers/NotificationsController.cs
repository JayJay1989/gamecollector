using GameCollector.Api.Authentication;
using GameCollector.Application.Notifications;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/me/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await notifications.ListAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        ToNoContent(await notifications.MarkReadAsync(id, cancellationToken));

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        ToNoContent(await notifications.MarkAllReadAsync(cancellationToken));

    private IActionResult ToNoContent(Application.Common.Result<bool> result) =>
        result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
}
