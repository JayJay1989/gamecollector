namespace GameCollector.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    string? Subject { get; }
    bool IsAdministrator { get; }
}
