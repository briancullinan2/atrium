namespace Clippy.Services;



public record ChatMessage(string Role, string Content);

public record ChatCompletionRequest(
    List<ChatMessage> Messages,
    double? Temperature = 1.0,
    int? MaxTokens = null,
    bool Stream = false
);

public record ChatCompletionResponse(
    List<ChatChoice> Choices,
    ChatUsage Usage
);

public record ChatChoice(ChatCompletionMessage Message, string FinishReason);
public record ChatCompletionMessage(string Role, string Content);
public record ChatUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

public record InitProgress(double Progress, string Text);

public record RuntimeStats(
    double PrefillTokensPerSec,
    double DecodeTokensPerSec,
    int TotalTokens
);



public interface IChatService
{
    string? ChatMessage { get; set; } // set the current message for feedback like auto complete
    Task<Tuple<bool, string>?> TryChat(ServicePreset? service);
    Task<List<ServicePreset>> ListPresets();
    Task<string?> SendMessage(string message);
    Task<bool?> IsWorking();
    Task SetChatMode(bool chat);
    bool Chat { get; set; }
    Dictionary<DateTime, Tuple<bool, string>>? Recents { get; }
    event Action<bool?>? OnChatWorking;
    event Action? OnChatMessage;
}
