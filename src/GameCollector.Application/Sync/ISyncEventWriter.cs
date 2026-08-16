namespace GameCollector.Application.Sync;

public interface ISyncEventWriter
{
    Task WriteAsync(string scopeType, Guid? scopeId, string operation, Guid entityId,
        object payload, CancellationToken cancellationToken = default);
}
