using GameCollector.Api.Authentication;
using GameCollector.Application.Catalog;
using GameCollector.Application.ExternalCatalog;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveDevice)]
[Route(ApiRoutes.V1)]
public sealed class CatalogController(ICatalogService catalog, IExternalProductLookupService externalLookup) : ControllerBase
{
    [HttpGet("games")]
    [HttpGet("games/search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken) => ToResponse(await catalog.SearchAsync(q, cancellationToken));

    [HttpGet("games/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => ToResponse(await catalog.GetAsync(id, cancellationToken));

    [HttpGet("games/barcode/{barcode}")]
    public async Task<IActionResult> Barcode(string barcode, CancellationToken cancellationToken) => ToResponse(await catalog.GetByBarcodeAsync(barcode, cancellationToken));

    [HttpGet("product-lookup/{barcode}")]
    public async Task<IActionResult> ProductLookup(string barcode, CancellationToken cancellationToken) => ToResponse(await externalLookup.LookupAsync(barcode, cancellationToken));

    [HttpGet("languages")]
    public async Task<IActionResult> Languages(CancellationToken cancellationToken) => ToResponse(await catalog.GetLanguagesAsync(cancellationToken));

    [HttpGet("tags")]
    public async Task<IActionResult> Tags(CancellationToken cancellationToken) => ToResponse(await catalog.GetTagsAsync(cancellationToken));

    private IActionResult ToResponse<T>(Application.Common.Result<T> result) => result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
}
