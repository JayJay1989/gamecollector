namespace GameCollector.Application.Abstractions.Auditing;

public interface IAuditContext
{
    string CorrelationId { get; }
    Guid? DeviceId { get; }
    string? IpAddress { get; }
}
