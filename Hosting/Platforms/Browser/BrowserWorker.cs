namespace Hosting.Platforms.Browser;

using Hosting.Services;
using Microsoft.JSInterop;
using System.Net.Http.Json;

public class BrowserWorker(IJSRuntime JS, HttpClient Http) : IServiceWorkerService, IAsyncDisposable
{
    private DotNetObjectReference<BrowserWorker>? _selfReference;
    private IJSObjectReference? Module;
    //private DotNetObjectReference<ServiceWorkerService>? _selfReference;

    public event Action<object>? OnMessageReceived;


    public async Task InitializeAsync()
    {
        Module = await JS.InvokeAsync<IJSObjectReference>("import", "./service.js");
        _selfReference = DotNetObjectReference.Create(this);
        await Module.InvokeVoidAsync("init", _selfReference);
    }

    [JSInvokable]
    public void ReceiveInternal(object data) => OnMessageReceived?.Invoke(data);

    public async Task<ServiceWorkerStatus> GetStatusAsync() =>
        await Module!.InvokeAsync<ServiceWorkerStatus>("getStatus");

    public async Task<bool> RegisterAsync(string? _, string scriptUrl) =>
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
        var serverHandshake = await _http.GetFromJsonAsync<long[]>(versionUrl + "?t=" + DateTime.Now.Ticks);
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
            await RegisterAsync(null, "/service-worker.published.js");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _selfReference?.Dispose();
        if (Module != null) await Module.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
