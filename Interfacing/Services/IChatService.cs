
namespace Interfacing.Services;


public interface IChatService
{
    string? ChatMessage { get; set; } // set the current message for feedback like auto complete
    Task<Tuple<bool, string>?> TryChat(object? service);
    Task<List<object>> ListPresets();
    Task<string?> SendMessage(string message);
    Task<bool?> IsWorking();
    Task SetChatMode(bool chat);
    bool Chat { get; set; }
    Dictionary<DateTime, Tuple<bool, string>>? Recents { get; }
    event Action<bool?>? OnChatWorking;
    event Action? OnChatMessage;
}
