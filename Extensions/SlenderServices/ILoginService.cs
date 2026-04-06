
namespace Extensions.SlenderServices
{
    public interface ILoginService
    {
        Task SetLoginMode(bool study);
        Task SetUser(object? user);
        bool Login { get; set; }
        object? User { get; set; }
        event Action<bool>? OnLoginChanged;
        event Action<object?>? OnUserChanged;
        bool IsReady { get; }
    }

}
