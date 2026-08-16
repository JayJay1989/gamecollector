using GameCollector.Api.Authentication;
using GameCollector.Application.Users;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameCollector.Application.Collections;
using GameCollector.Contracts.Collections;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/me")]
public sealed class ProfileController(IProfileService profiles, ICollectionService collections) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await profiles.GetCurrentAsync(cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.ToProblemResult(result.Error!);
    }

    [HttpPost("onboarding")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Onboard(
        OnboardUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await profiles.OnboardAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetCurrent), result.Value)
            : this.ToProblemResult(result.Error!);
    }

    [HttpPatch]
    [Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await profiles.UpdateAsync(request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.ToProblemResult(result.Error!);
    }

    [HttpPut("default-collection")]
    [Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
    public async Task<IActionResult> SetDefaultCollection(
        SetDefaultCollectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await collections.SetDefaultAsync(request.CollectionId, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
    }
}
