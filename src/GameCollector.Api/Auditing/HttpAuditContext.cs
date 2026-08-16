using GameCollector.Api.Middleware;
using GameCollector.Application.Abstractions.Auditing;
using GameCollector.Contracts.Users;

namespace GameCollector.Api.Auditing;

public sealed class HttpAuditContext(IHttpContextAccessor accessor) : IAuditContext
{
    private HttpContext Context => accessor.HttpContext ?? throw new InvalidOperationException("No active HTTP request.");
    public string CorrelationId => Context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
        ? value?.ToString() ?? Context.TraceIdentifier : Context.TraceIdentifier;
    public Guid? DeviceId => Guid.TryParse(Context.Request.Headers[DeviceHeaders.DeviceId].FirstOrDefault(), out var id) ? id : null;
    public string? IpAddress => Context.Connection.RemoteIpAddress?.ToString();
}
