using Microsoft.JSInterop;

namespace Clippy.Services;

internal class LocalService(IJSRuntime js) : IAsyncDisposable
{

    private IJSObjectReference? _module;
    private IJSObjectReference? _engine;

    public event Action<InitProgress>? OnLoadingProgress;

    public async Task InitializeAsync(string modelId)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./web-llm.js");

        // Wrap 'this' to receive progress callbacks
        using var dotNetRef = DotNetObjectReference.Create(this);

        _engine = await _module.InvokeAsync<IJSObjectReference>("createEngine", modelId, dotNetRef);
    }

    [JSInvokable]
    public void OnProgress(double progress, string text)
        => OnLoadingProgress?.Invoke(new InitProgress(progress, text));

    public async Task<ChatCompletionResponse?> ChatAsync(ChatCompletionRequest request)
    {
        if (_module == null || _engine == null) return null;
        return await _module.InvokeAsync<ChatCompletionResponse>("chatCompletion", _engine, request);
    }

    public async Task<RuntimeStats?> GetStatsAsync()
    {
        if (_module == null || _engine == null) return null;
        return await _module.InvokeAsync<RuntimeStats>("getStats", _engine);
    }

    public async Task InterruptAsync() => await _module!.InvokeVoidAsync("interrupt", _engine);

    public async ValueTask DisposeAsync()
    {
        if (_module != null && _engine != null)
            await _module.InvokeVoidAsync("unload", _engine);

        if(_engine != null)
            await _engine.DisposeAsync();
        if(_module != null)
            await _module.DisposeAsync();
    }
}


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


