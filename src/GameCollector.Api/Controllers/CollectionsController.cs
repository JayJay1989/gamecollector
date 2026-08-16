using GameCollector.Api.Authentication;
using GameCollector.Application.Collections;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1 + "/collections")]
public sealed class CollectionsController(ICollectionService service, IOwnershipService ownership) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => ToResponse(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateCollectionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => ToResponse(await service.GetAsync(id, cancellationToken));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCollectionRequest request, CancellationToken cancellationToken) => ToResponse(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) => ToNoContent(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, TransferOwnershipRequest request, CancellationToken cancellationToken) => ToResponse(await service.TransferOwnershipAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken cancellationToken) => ToResponse(await service.GetMembersAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, Guid userId, UpdateCollectionMemberRequest request, CancellationToken cancellationToken) => ToNoContent(await service.UpdateMemberAsync(id, userId, request, cancellationToken));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken) => ToNoContent(await service.RemoveMemberAsync(id, userId, cancellationToken));

    [HttpPost("{id:guid}/invitations")]
    public async Task<IActionResult> Invite(Guid id, CreateCollectionInvitationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.InviteAsync(id, request, cancellationToken);
        return result.IsSuccess ? Created(ApiRoutes.V1 + "/invitations/" + result.Value!.Id, result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpGet("{id:guid}/games")]
    public async Task<IActionResult> Games(Guid id, CancellationToken cancellationToken) => ToResponse(await ownership.GetCollectionGamesAsync(id, cancellationToken));

    [HttpPut("{id:guid}/games/{gameId:guid}")]
    public async Task<IActionResult> AddGame(Guid id, Guid gameId, CancellationToken cancellationToken) => ToNoContent(await ownership.AddToCollectionAsync(id, gameId, cancellationToken));

    [HttpDelete("{id:guid}/games/{gameId:guid}")]
    public async Task<IActionResult> RemoveGame(Guid id, Guid gameId, CancellationToken cancellationToken) => ToNoContent(await ownership.RemoveFromCollectionAsync(id, gameId, cancellationToken));

    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    private IActionResult ToNoContent(Application.Common.Result<bool> result) => result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error!);
}
