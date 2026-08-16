using GameCollector.Domain.Background;

namespace GameCollector.Domain.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void FailureSchedulesExponentialRetryAndCompletionClearsError()
    {
        var now = DateTime.UtcNow;
        var message = OutboxMessage.Create(Guid.NewGuid(), "Test", "{}", now);

        message.Fail("Temporary failure", now);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(now.AddSeconds(2), message.NextAttemptAtUtc);
        Assert.Equal("Temporary failure", message.LastError);

        message.Complete(now.AddSeconds(3));
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.LastError);
    }
}
