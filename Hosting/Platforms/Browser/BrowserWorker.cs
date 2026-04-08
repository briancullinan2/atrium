namespace Hosting.Platforms.Browser;

using Hosting.Services;
using Microsoft.JSInterop;
using System.Net.Http.Json;

public class BrowserWorker : IServiceWorkerService, IAsyncDisposable
{
    private DotNetObjectReference<BrowserWorker>? _selfReference;
    private IJSObjectReference? _module = null;
    public IJSObjectReference Module
    {
        get
        {
            if (!_renderTcs.Task.IsCompleted || _module == null)
            {
                throw new InvalidOperationException("Module is not available. Must await ModuleInitialize before refering to JS module.");
            }
            return _module;
        }
        private set => _module = value;
    }
    //private DotNetObjectReference<ServiceWorkerService>? _selfReference;
    public bool IsReady => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;
    private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IJSRuntime JS;
    private readonly HttpClient Http;
    private readonly IRenderState Rendered;

    public event Action<object?>? OnMessageReceived;
    public Task ModuleInitialize => _renderTcs.Task;

    public BrowserWorker(IJSRuntime _js, HttpClient _http, IRenderState _rendered)
    {
        JS = _js;
        Http = _http;
        Rendered = _rendered;
        // Start the import immediately
        Rendered.OnRendered += NotifyRendered;
        Rendered.OnEmptied += NotifyEmptied;
    }

    protected void NotifyEmptied()
    {
        if (_renderTcs.Task.IsCompleted)
            _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    protected void NotifyRendered() => _ = InitializeAsync();

    public async Task InitializeAsync()
    {
        Module = await JS.InvokeAsync<IJSObjectReference>("import", "./service.js");
        _selfReference = DotNetObjectReference.Create(this);
        await Module.InvokeVoidAsync("init", _selfReference);
    }

    [JSInvokable]
    public void ReceiveInternal(object? data) => OnMessageReceived?.Invoke(data);

    public async Task<ServiceWorkerStatus> GetStatusAsync() =>
        await Module!.InvokeAsync<ServiceWorkerStatus>("getStatus");

    public async Task<bool> RegisterAsync(string _, string? a = null, string? scriptUrl = null) =>
        await Module!.InvokeAsync<bool>("register", scriptUrl);


    public async Task<bool> UnregisterAsync() =>
        await Module!.InvokeAsync<bool>("unregister");

    public async Task<TResponse?> PostMessageAsync<TRequest, TResponse>(TRequest message, int timeoutMs = 10000) =>
        await Module!.InvokeAsync<TResponse>("postMessageWithResponse", message, timeoutMs);

    public async Task<long?> GetVersionAsync()
    {
        try
        {
            // Replicates your 'GET_VERSION' -> 'VERSION_REPORT' flow
            var response = await PostMessageAsync<object, dynamic>(new { type = "GET_VERSION" });
            if (response == null) return null;

            // Assuming version is returned as a JSON array [startTime, ticks]
            return (long)response.version[1];
        }
        catch { return null; }
    }

    /// <summary>
    /// The "Nuke and Pave" logic from your JS, now controlled by C#.
    /// </summary>
    public async Task ForceSyncVersionAsync(string versionUrl)
    {
        // 1. Get Server Truth
        var serverHandshake = await Http.GetFromJsonAsync<long[]>(versionUrl + "?t=" + DateTime.Now.Ticks);
        if (serverHandshake == null) return;
        long serverVersion = serverHandshake[1];

        var status = await GetStatusAsync();
        if (status.IsActive)
        {
            // 2. Get SW Truth
            long? swVersion = await GetVersionAsync();

            // 3. Compare and Nuke if mismatched
            if (swVersion.HasValue && swVersion != serverVersion)
            {
                await PostMessageAsync<object, object>(new { type = "DEREGISTER" });
                await UnregisterAsync();
            }
        }

        // 4. Re-register if needed
        status = await GetStatusAsync();
        if (!status.IsActive)
        {
            await RegisterAsync(string.Empty, null, "/service-worker.published.js");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _selfReference?.Dispose();
        if (Module != null) await Module.DisposeAsync();
        GC.SuppressFinalize(this);
    }

}
