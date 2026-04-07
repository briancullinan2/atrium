
namespace Extensions.SlenderServices;

public interface ILoginService
{
    Task SetLoginMode(bool study);
    Task SetUser(object? user);
    bool Login { get; }
    string? UserId { get; }
    object? User { get; }
    event Action<bool>? OnLoginChanged;
    event Action<object?>? OnUserChanged;
    bool IsReady { get; }
}
