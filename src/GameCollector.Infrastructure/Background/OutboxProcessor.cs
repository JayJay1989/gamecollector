using GameCollector.Application.Abstractions.Background;
using GameCollector.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameCollector.Infrastructure.Background;

public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly Action<ILogger, Guid, string, Exception?> LogFailure = LoggerMessage.Define<Guid, string>(
        LogLevel.Error, new EventId(1, "OutboxProcessingFailed"), "Outbox message {MessageId} of type {MessageType} failed");
    private static readonly Action<ILogger, Exception?> LogWorkerFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(2, "OutboxWorkerFailed"), "The outbox worker iteration failed and will be retried");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed) await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogWorkerFailure(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var message = await repository.GetNextDueAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (message is null) return false;
        var handler = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
            .SingleOrDefault(item => string.Equals(item.MessageType, message.Type, StringComparison.Ordinal));
        try
        {
            if (handler is null) throw new InvalidOperationException($"No handler is registered for '{message.Type}'.");
            await handler.HandleAsync(message.PayloadJson, cancellationToken);
            message.Complete(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(logger, message.Id, message.Type, exception);
            var error = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
            message.Fail(error, timeProvider.GetUtcNow().UtcDateTime);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
