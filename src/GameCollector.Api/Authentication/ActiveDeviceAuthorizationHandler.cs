using GameCollector.Application.Users;
using GameCollector.Contracts.Users;
using Microsoft.AspNetCore.Authorization;

namespace GameCollector.Api.Authentication;

public sealed class ActiveDeviceAuthorizationHandler(IDeviceService devices)
    : AuthorizationHandler<ActiveDeviceRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveDeviceRequirement requirement)
    {
        var httpContext = context.Resource as HttpContext;
        var subject = context.User.FindFirst("sub")?.Value;
        var suppliedDeviceId = httpContext?.Request.Headers[DeviceHeaders.DeviceId].FirstOrDefault();

        if (subject is not null &&
            Guid.TryParse(suppliedDeviceId, out var deviceId) &&
            await devices.IsActiveAsync(subject, deviceId, httpContext?.RequestAborted ?? default))
        {
            context.Succeed(requirement);
        }
    }
}
