using GameCollector.Domain.Common;

namespace GameCollector.Domain.Catalog;

public enum GameChangeRequestStatus { Pending, Approved, Rejected }

public sealed class GameChangeRequest
{
    private GameChangeRequest() { }
    private GameChangeRequest(Guid id, Guid gameId, Guid proposedByUserId, string proposedChangesJson, DateTime createdAtUtc)
    {
        Id = id; GameId = gameId; ProposedByUserId = proposedByUserId;
        ProposedChangesJson = Required(proposedChangesJson, 16000, "Proposed changes");
        Status = GameChangeRequestStatus.Pending;
        CreatedAtUtc = Utc(createdAtUtc); UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public Guid ProposedByUserId { get; private set; }
    public string ProposedChangesJson { get; private set; } = string.Empty;
    public GameChangeRequestStatus Status { get; private set; }
    public string? AdminComment { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Game Game { get; private set; } = null!;

    public static GameChangeRequest Create(Guid id, Guid gameId, Guid userId, string proposedChangesJson, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || gameId == Guid.Empty || userId == Guid.Empty)
            throw new DomainValidationException("Valid change-request IDs are required.");
        return new GameChangeRequest(id, gameId, userId, proposedChangesJson, createdAtUtc);
    }

    public void Approve(Guid administratorId, string? comment, DateTime reviewedAtUtc) => Review(GameChangeRequestStatus.Approved, administratorId, comment, reviewedAtUtc);
    public void Reject(Guid administratorId, string comment, DateTime reviewedAtUtc) => Review(GameChangeRequestStatus.Rejected, administratorId, Required(comment, 2000, "Rejection reason"), reviewedAtUtc);

    private void Review(GameChangeRequestStatus status, Guid administratorId, string? comment, DateTime reviewedAtUtc)
    {
        if (Status != GameChangeRequestStatus.Pending) throw new DomainValidationException("The change request has already been reviewed.");
        if (administratorId == Guid.Empty) throw new DomainValidationException("An administrator ID is required.");
        Status = status; ReviewedByUserId = administratorId; AdminComment = Optional(comment, 2000);
        ReviewedAtUtc = Utc(reviewedAtUtc); UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int max, string name) { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainValidationException($"{name} is invalid."); return result; }
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Required(value, max, "Comment");
}
