using System;
using System.Collections.Generic;
using System.Text;

namespace Hosting.Platforms.Browser
{
    using Hosting.Services;
    using Microsoft.JSInterop;
    using System.Net.Http.Json;

    public class BrowserWorker : IServiceWorkerService, IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private readonly HttpClient _http;
        private IJSObjectReference? _module;
        //private DotNetObjectReference<ServiceWorkerService>? _selfReference;

        public event Action<object>? OnMessageReceived;

        public ServiceWorkerService(IJSRuntime js, HttpClient http)
        {
            _js = js;
            _http = http;
        }

        public async Task InitializeAsync()
        {
            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./service.js");
            _selfReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("init", _selfReference);
        }

        [JSInvokable]
        public void ReceiveInternal(object data) => OnMessageReceived?.Invoke(data);

        public async Task<ServiceWorkerStatus> GetStatusAsync() =>
            await _module!.InvokeAsync<ServiceWorkerStatus>("getStatus");

        public async Task<SwRegistrationResult> RegisterAsync(string scriptUrl) =>
            await _module!.InvokeAsync<SwRegistrationResult>("register", scriptUrl);

        public async Task<bool> UnregisterAsync() =>
            await _module!.InvokeAsync<bool>("unregister");

        public async Task<TResponse?> PostMessageAsync<TRequest, TResponse>(TRequest message, int timeoutMs = 10000) =>
            await _module!.InvokeAsync<TResponse>("postMessageWithResponse", message, timeoutMs);

        public async Task<long?> GetSwVersionAsync()
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
                long? swVersion = await GetSwVersionAsync();

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
                await RegisterAsync("/service-worker.published.js");
            }
        }

        public async ValueTask DisposeAsync()
        {
            _selfReference?.Dispose();
            if (_module != null) await _module.DisposeAsync();
        }
    }
}
