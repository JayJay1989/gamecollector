using System.Text.Json;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Sync;

namespace GameCollector.Application.Sync;

public sealed class SyncEventWriter(ISyncRepository repository, TimeProvider timeProvider) : ISyncEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task WriteAsync(string scopeType, Guid? scopeId, string operation, Guid entityId,
        object payload, CancellationToken cancellationToken = default) =>
        await repository.AddEventAsync(SyncEvent.Create(scopeType, scopeId, operation, entityId,
            JsonSerializer.Serialize(payload, JsonOptions), timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
}
