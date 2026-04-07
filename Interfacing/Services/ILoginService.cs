namespace Interfacing.Services;

public interface ILoginService
{
    Task SetLoginMode(bool study);
    Task SetUser(object? user);
    bool Login { get; }
    List<string>? Roles { get; }
    Dictionary<string, string?>? Permissions { get; }
    string? UserId { get; }
    object? User { get; }
    event Action<bool>? OnLoginChanged;
    event Action<object?>? OnUserChanged;
    bool IsReady { get; }
}
