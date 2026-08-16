using GameCollector.Application.Abstractions.Background;
using GameCollector.Domain.Background;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository(ApplicationDbContext dbContext, TimeProvider timeProvider) : IOutboxRepository, IOutboxWriter
{
    public async Task EnqueueAsync(string type, string payloadJson, CancellationToken cancellationToken = default) =>
        await dbContext.OutboxMessages.AddAsync(OutboxMessage.Create(Guid.NewGuid(), type, payloadJson, timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
    public Task<OutboxMessage?> GetNextDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default) =>
        dbContext.OutboxMessages.Where(item => item.ProcessedAtUtc == null && item.NextAttemptAtUtc <= nowUtc)
            .OrderBy(item => item.OccurredAtUtc).FirstOrDefaultAsync(cancellationToken);
}
