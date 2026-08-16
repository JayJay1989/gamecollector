using GameCollector.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace GameCollector.Api.Authentication;

public sealed class EnabledApplicationUserAuthorizationHandler(IUserProfileRepository users)
    : AuthorizationHandler<EnabledApplicationUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        EnabledApplicationUserRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (subject is null) return;
        var httpContext = context.Resource as HttpContext;
        var profile = await users.GetBySubjectAsync(subject, httpContext?.RequestAborted ?? default);
        if (profile is null || !profile.IsDisabled) context.Succeed(requirement);
    }
}
