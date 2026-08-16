using System.ComponentModel.DataAnnotations;
using GameCollector.Api.Authentication;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Timeouts;

namespace GameCollector.Api.Tests.Infrastructure;

[ApiController]
[Route(ApiRoutes.V1 + "/test/security")]
public sealed class SecurityProbeController : ControllerBase
{
    [Authorize]
    [HttpGet("user")]
    public IActionResult UserEndpoint() => Ok();

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpGet("admin")]
    public IActionResult AdministratorEndpoint() => Ok();

    [AllowAnonymous]
    [HttpPost("validation")]
    public IActionResult ValidationEndpoint(ValidationRequest request) => Ok(request);

    [AllowAnonymous]
    [HttpGet("exception")]
    public IActionResult ExceptionEndpoint()
    {
        _ = Request.Path;
        throw new InvalidOperationException("Sensitive test detail");
    }

    [AllowAnonymous]
    [HttpGet("slow")]
    [RequestTimeout("ShortIntegrationTest")]
    public async Task<IActionResult> SlowEndpoint(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        return Ok();
    }
}

public sealed record ValidationRequest([Required, MinLength(3)] string? Name);
