using GameCollector.Domain.Common;

namespace GameCollector.Domain.Catalog;

public sealed class Game
{
    private readonly List<GameBarcode> _barcodes = [];
    private readonly List<GameLanguage> _languages = [];
    private readonly List<GameTag> _tags = [];
    private Game() { }

    private Game(Guid id, string title, string? description, string? publisher, int? releaseYear,
        int? minimumPlayers, int? maximumPlayers, int? minimumAge, int? minimumPlayingTimeMinutes,
        int? maximumPlayingTimeMinutes, ModerationStatus status, Guid? submittedByUserId, DateTime createdAtUtc)
    {
        Id = id;
        SetDetails(title, description, publisher, releaseYear, minimumPlayers, maximumPlayers,
            minimumAge, minimumPlayingTimeMinutes, maximumPlayingTimeMinutes);
        ModerationStatus = status;
        SubmittedByUserId = submittedByUserId;
        CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        Revision = 1;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Publisher { get; private set; }
    public int? ReleaseYear { get; private set; }
    public int? MinimumPlayers { get; private set; }
    public int? MaximumPlayers { get; private set; }
    public int? MinimumAge { get; private set; }
    public int? MinimumPlayingTimeMinutes { get; private set; }
    public int? MaximumPlayingTimeMinutes { get; private set; }
    public ModerationStatus ModerationStatus { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public long Revision { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? ModerationComment { get; private set; }
    public IReadOnlyCollection<GameBarcode> Barcodes => _barcodes.AsReadOnly();
    public IReadOnlyCollection<GameLanguage> Languages => _languages.AsReadOnly();
    public IReadOnlyCollection<GameTag> Tags => _tags.AsReadOnly();

    public static Game Create(Guid id, string title, string? description, string? publisher,
        int? releaseYear, int? minimumPlayers, int? maximumPlayers, int? minimumAge,
        int? minimumPlayingTimeMinutes, int? maximumPlayingTimeMinutes,
        ModerationStatus status, Guid? submittedByUserId, DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new DomainValidationException("A game ID is required.");
        if (status != ModerationStatus.Approved && submittedByUserId is null)
            throw new DomainValidationException("A non-approved game requires a submitter.");
        return new Game(id, title, description, publisher, releaseYear, minimumPlayers, maximumPlayers,
            minimumAge, minimumPlayingTimeMinutes, maximumPlayingTimeMinutes, status, submittedByUserId, createdAtUtc);
    }

    public static Game CreateApproved(Guid id, string title, string? description, string? publisher,
        int? releaseYear, int? minimumPlayers, int? maximumPlayers, int? minimumAge,
        int? minimumPlayingTimeMinutes, int? maximumPlayingTimeMinutes,
        Guid administratorId, DateTime createdAtUtc)
    {
        if (administratorId == Guid.Empty) throw new DomainValidationException("An administrator ID is required.");
        var game = Create(id, title, description, publisher, releaseYear, minimumPlayers, maximumPlayers,
            minimumAge, minimumPlayingTimeMinutes, maximumPlayingTimeMinutes,
            ModerationStatus.Approved, null, createdAtUtc);
        game.ApprovedByUserId = administratorId;
        game.ApprovedAtUtc = game.CreatedAtUtc;
        return game;
    }

    public void AddBarcode(Guid id, string barcode) => _barcodes.Add(GameBarcode.Create(id, Id, barcode));
    public void AddLanguage(Guid languageId) { if (_languages.All(item => item.LanguageId != languageId)) _languages.Add(new GameLanguage(Id, languageId)); }
    public void AddTag(Guid tagId) { if (_tags.All(item => item.TagId != tagId)) _tags.Add(new GameTag(Id, tagId)); }

    public void UpdateSubmission(string title, string? description, string? publisher, int? releaseYear,
        int? minimumPlayers, int? maximumPlayers, int? minimumAge, int? minimumPlayingTimeMinutes,
        int? maximumPlayingTimeMinutes, IEnumerable<string> barcodes, IEnumerable<Guid> languageIds,
        IEnumerable<Guid> tagIds, DateTime updatedAtUtc)
    {
        if (ModerationStatus is not (ModerationStatus.Draft or ModerationStatus.NeedsChanges))
            throw new DomainValidationException("Only draft submissions or submissions needing changes can be edited.");
        SetDetails(title, description, publisher, releaseYear, minimumPlayers, maximumPlayers,
            minimumAge, minimumPlayingTimeMinutes, maximumPlayingTimeMinutes);
        _barcodes.Clear();
        foreach (var barcode in barcodes.Distinct(StringComparer.Ordinal)) AddBarcode(Guid.NewGuid(), barcode);
        _languages.Clear();
        foreach (var id in languageIds.Distinct()) AddLanguage(id);
        _tags.Clear();
        foreach (var id in tagIds.Distinct()) AddTag(id);
        Touch(updatedAtUtc);
    }

    public void Submit(DateTime updatedAtUtc)
    {
        if (ModerationStatus is not (ModerationStatus.Draft or ModerationStatus.NeedsChanges))
            throw new DomainValidationException("This submission cannot be submitted for review.");
        ModerationStatus = ModerationStatus.Pending;
        ModerationComment = null;
        Touch(updatedAtUtc);
    }

    public void Approve(Guid administratorId, DateTime updatedAtUtc)
    {
        EnsurePending(administratorId);
        ModerationStatus = ModerationStatus.Approved;
        ApprovedByUserId = administratorId;
        ApprovedAtUtc = Utc(updatedAtUtc);
        ModerationComment = null;
        Touch(updatedAtUtc);
    }

    public void RequestChanges(Guid administratorId, string comment, DateTime updatedAtUtc)
    {
        EnsurePending(administratorId);
        ModerationComment = Required(comment, 2000, "Moderation comment");
        ModerationStatus = ModerationStatus.NeedsChanges;
        Touch(updatedAtUtc);
    }

    public void Reject(Guid administratorId, string reason, DateTime updatedAtUtc)
    {
        EnsurePending(administratorId);
        ModerationComment = Required(reason, 2000, "Rejection reason");
        ModerationStatus = ModerationStatus.Rejected;
        Touch(updatedAtUtc);
    }

    public void ApplyApprovedCorrection(string? title, string? description, string? publisher,
        int? releaseYear, int? minimumPlayers, int? maximumPlayers, int? minimumAge,
        int? minimumPlayingTimeMinutes, int? maximumPlayingTimeMinutes, DateTime updatedAtUtc)
    {
        if (ModerationStatus != ModerationStatus.Approved)
            throw new DomainValidationException("Corrections can only be applied to approved games.");
        SetDetails(title ?? Title, description ?? Description, publisher ?? Publisher,
            releaseYear ?? ReleaseYear, minimumPlayers ?? MinimumPlayers, maximumPlayers ?? MaximumPlayers,
            minimumAge ?? MinimumAge, minimumPlayingTimeMinutes ?? MinimumPlayingTimeMinutes,
            maximumPlayingTimeMinutes ?? MaximumPlayingTimeMinutes);
        Touch(updatedAtUtc);
    }

    public void UpdateApproved(string title, string? description, string? publisher,
        int? releaseYear, int? minimumPlayers, int? maximumPlayers, int? minimumAge,
        int? minimumPlayingTimeMinutes, int? maximumPlayingTimeMinutes,
        IEnumerable<string> barcodes, IEnumerable<Guid> languageIds, IEnumerable<Guid> tagIds,
        DateTime updatedAtUtc)
    {
        if (ModerationStatus != ModerationStatus.Approved)
            throw new DomainValidationException("Only approved games can be edited through catalog administration.");
        SetDetails(title, description, publisher, releaseYear, minimumPlayers, maximumPlayers,
            minimumAge, minimumPlayingTimeMinutes, maximumPlayingTimeMinutes);
        _barcodes.Clear();
        foreach (var barcode in barcodes.Distinct(StringComparer.Ordinal)) AddBarcode(Guid.NewGuid(), barcode);
        _languages.Clear();
        foreach (var id in languageIds.Distinct()) AddLanguage(id);
        _tags.Clear();
        foreach (var id in tagIds.Distinct()) AddTag(id);
        Touch(updatedAtUtc);
    }

    private void SetDetails(string title, string? description, string? publisher, int? releaseYear,
        int? minimumPlayers, int? maximumPlayers, int? minimumAge, int? minimumPlayingTimeMinutes,
        int? maximumPlayingTimeMinutes)
    {
        Title = Required(title, 200, "Game title");
        Description = Optional(description, 4000);
        Publisher = Optional(publisher, 200);
        if (releaseYear is < 1800 or > 2200) throw new DomainValidationException("Release year is invalid.");
        ValidateRange(minimumPlayers, maximumPlayers, "player count");
        ValidateRange(minimumPlayingTimeMinutes, maximumPlayingTimeMinutes, "playing time");
        if (minimumAge is < 0 or > 100) throw new DomainValidationException("Minimum age is invalid.");
        ReleaseYear = releaseYear; MinimumPlayers = minimumPlayers; MaximumPlayers = maximumPlayers;
        MinimumAge = minimumAge; MinimumPlayingTimeMinutes = minimumPlayingTimeMinutes;
        MaximumPlayingTimeMinutes = maximumPlayingTimeMinutes;
    }

    private static void ValidateRange(int? minimum, int? maximum, string name)
    {
        if (minimum is < 1 || maximum is < 1 || (minimum.HasValue && maximum.HasValue && minimum > maximum))
            throw new DomainValidationException($"The {name} range is invalid.");
    }
    private static string Required(string value, int max, string name) { var trimmed = value.Trim(); if (trimmed.Length is < 1 || trimmed.Length > max) throw new DomainValidationException($"{name} is invalid."); return trimmed; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var trimmed = value.Trim(); if (trimmed.Length > max) throw new DomainValidationException("A game field is too long."); return trimmed; }
    private void EnsurePending(Guid administratorId)
    {
        if (administratorId == Guid.Empty) throw new DomainValidationException("An administrator ID is required.");
        if (ModerationStatus != ModerationStatus.Pending) throw new DomainValidationException("Only pending submissions can be moderated.");
    }
    private void Touch(DateTime updatedAtUtc) { UpdatedAtUtc = Utc(updatedAtUtc); Revision++; }
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
