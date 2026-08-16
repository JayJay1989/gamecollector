using GameCollector.Domain.Common;

namespace GameCollector.Domain.Sync;

public sealed class ProcessedMutation
{
    private ProcessedMutation() { }
    private ProcessedMutation(Guid mutationId, Guid userId, DateTime processedAtUtc, string resultJson)
    { MutationId = mutationId; UserId = userId; ProcessedAtUtc = processedAtUtc.Kind == DateTimeKind.Utc ? processedAtUtc : processedAtUtc.ToUniversalTime(); ResultJson = resultJson; }
    public Guid MutationId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }
    public string ResultJson { get; private set; } = string.Empty;
    public static ProcessedMutation Create(Guid mutationId, Guid userId, DateTime processedAtUtc, string resultJson)
    {
        if (mutationId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(resultJson) || resultJson.Length > 8000)
            throw new DomainValidationException("The processed mutation is invalid.");
        return new ProcessedMutation(mutationId, userId, processedAtUtc, resultJson);
    }
}
