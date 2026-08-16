namespace GameCollector.Domain.Catalog;

public sealed class Language
{
    private Language() { }
    public Language(Guid id, string code, string name) { Id = id; Code = code; Name = name; }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
}

public sealed class Tag
{
    private Tag() { }
    public Tag(Guid id, string name) { Id = id; Name = name; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
}

public sealed class GameLanguage
{
    private GameLanguage() { }
    public GameLanguage(Guid gameId, Guid languageId) { GameId = gameId; LanguageId = languageId; }
    public Guid GameId { get; private set; }
    public Guid LanguageId { get; private set; }
    public Game Game { get; private set; } = null!;
    public Language Language { get; private set; } = null!;
}

public sealed class GameTag
{
    private GameTag() { }
    public GameTag(Guid gameId, Guid tagId) { GameId = gameId; TagId = tagId; }
    public Guid GameId { get; private set; }
    public Guid TagId { get; private set; }
    public Game Game { get; private set; } = null!;
    public Tag Tag { get; private set; } = null!;
}
