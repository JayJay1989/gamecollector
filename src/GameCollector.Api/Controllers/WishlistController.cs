using GameCollector.Api.Authentication;
using GameCollector.Application.Collections;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/me/wishlist")]
public sealed class WishlistController(IOwnershipService ownership) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await ownership.GetWishlistAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }
    [HttpPut("{gameId:guid}")]
    public async Task<IActionResult> Add(Guid gameId, CancellationToken cancellationToken) => ToNoContent(await ownership.AddToWishlistAsync(gameId, cancellationToken));
    [HttpDelete("{gameId:guid}")]
    public async Task<IActionResult> Remove(Guid gameId, CancellationToken cancellationToken) => ToNoContent(await ownership.RemoveFromWishlistAsync(gameId, cancellationToken));
    private IActionResult ToNoContent(Application.Common.Result<bool> result) => result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
}
