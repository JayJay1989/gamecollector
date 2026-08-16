using GameCollector.Api.Authentication;
using GameCollector.Application.Collections;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/users")]
public sealed class UsersController(ICollectionService service) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string type, [FromQuery] string q, CancellationToken cancellationToken)
    {
        var result = await service.SearchUsersAsync(q, string.Equals(type, "username", StringComparison.OrdinalIgnoreCase), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }
}
