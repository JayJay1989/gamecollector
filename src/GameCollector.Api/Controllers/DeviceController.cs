using GameCollector.Api.Authentication;
using GameCollector.Application.Users;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/me/device")]
public sealed class DeviceController(IDeviceService devices) : ControllerBase
{
    [HttpPost("activate")]
    [ProducesResponseType<DeviceRegistrationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(
        ActivateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await devices.ActivateAsync(request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.ToProblemResult(result.Error!);
    }

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        var result = await devices.RevokeAsync(cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : this.ToProblemResult(result.Error!);
    }
}
