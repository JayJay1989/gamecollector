using GameCollector.Api.Authentication;
using GameCollector.Application.Collections;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/friends")]
public sealed class FriendsController(IFriendService friends) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken token) => ToResponse(await friends.ListAsync(token));
    [HttpGet("requests")] public async Task<IActionResult> Requests(CancellationToken token) => ToResponse(await friends.ListRequestsAsync(token));
    [HttpPost("requests")] public async Task<IActionResult> Send(CreateFriendRequest request, CancellationToken token)
    { var result = await friends.SendRequestAsync(request, token); return result.IsSuccess ? Created($"{ApiRoutes.V1}/friends/requests/{result.Value!.Id}", result.Value) : this.ToProblemResult(result.Error!); }
    [HttpPost("requests/{id:guid}/accept")] public async Task<IActionResult> Accept(Guid id, CancellationToken token) => ToNoContent(await friends.RespondAsync(id, true, token));
    [HttpPost("requests/{id:guid}/decline")] public async Task<IActionResult> Decline(Guid id, CancellationToken token) => ToNoContent(await friends.RespondAsync(id, false, token));
    [HttpDelete("{userId:guid}")] public async Task<IActionResult> Remove(Guid userId, CancellationToken token) => ToNoContent(await friends.RemoveAsync(userId, token));
    [HttpGet("{userId:guid}")] public async Task<IActionResult> Profile(Guid userId, CancellationToken token) => ToResponse(await friends.GetProfileAsync(userId, token));
    [HttpGet("{userId:guid}/collections/{collectionId:guid}/games")] public async Task<IActionResult> Games(Guid userId, Guid collectionId, CancellationToken token) => ToResponse(await friends.GetCollectionGamesAsync(userId, collectionId, token));
    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    private IActionResult ToNoContent(Application.Common.Result<bool> result) => result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
}
