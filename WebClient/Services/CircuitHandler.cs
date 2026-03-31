using DataLayer.Utilities;
using FlashCard.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebClient.Services
{
    public class CircuitHandler : IConnectionStateProvider, IAsyncDisposable
    {
        public event Action<bool, ConnectionMetadata>? OnConnectionDown;
        public event Action<bool, ConnectionMetadata>? OnConnectionUp;

        public bool IsConnected => _connection.State == HubConnectionState.Connected;
        public int ClientCount => 1;

        private IRenderStateProvider Rendered { get; }
        private Task ModuleInitialize { get => _restartRequired.Task; }

        protected void NotifyEmptied()
        {
            if (_restartRequired.Task.IsCompleted)
                _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        protected void NotifyRendered() => _ = EnsureModuleLoaded();


        private IJSObjectReference? _module = null;
        private TaskCompletionSource<bool> _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IJSObjectReference Module
        {
            get
            {
                if (!_restartRequired.Task.IsCompleted || _module == null)
                {
                    throw new InvalidOperationException("Module is not available. Must await ModuleInitialize before refering to JS module.");
                }
                return _module;
            }
            private set => _module = value;
        }


        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private async Task EnsureModuleLoaded()
        {
            // 1. Quick check outside the lock for performance
            if (_restartRequired.Task.IsCompleted) return;

            // 2. Wait for the lock
            await _loadLock.WaitAsync();

            try
            {
                if (_restartRequired.Task.IsCompleted) return;
                await PageManager.EnsureModuleLoaded();
                _restartRequired.TrySetResult(true);
                PageManager.OnReconnect["window"] += ReportFromPage;

            }
            finally
            {
                // 4. Always release the lock in a finally block
                _loadLock.Release();
            }
        }


        protected void ReportFromPage(string id, string? state)
        {
            if (state == "hide")
            {
                OnConnectionUp?.Invoke(true, new ConnectionMetadata(_connection.ConnectionId ?? "unknown", DateTime.UtcNow));
            }
            else
            {
                OnConnectionDown?.Invoke(false, new ConnectionMetadata(_connection.ConnectionId ?? "unknown", DateTime.UtcNow, state));
            }
        }


        public IPageManager PageManager { get; }

        private readonly HubConnection _connection;

        public CircuitHandler(IPageManager page, HubConnection connection, IRenderStateProvider rendered)
        {
            Rendered = rendered;
            Rendered.OnEmptied += NotifyEmptied;
            Rendered.OnRendered += NotifyRendered;
            PageManager = page;

            _connection = connection;
            _connection.StartAsync();

            _connection.Reconnected += async (id) =>
                OnConnectionUp?.Invoke(true, new ConnectionMetadata(id ?? _connection.ConnectionId ?? "unknown", DateTime.UtcNow));

            _connection.Closed += async (ex) =>
                OnConnectionDown?.Invoke(false, new ConnectionMetadata(_connection.ConnectionId ?? "unknown", DateTime.UtcNow, ex?.Message, ex));
        }


        public async ValueTask DisposeAsync()
        {
            PageManager.OnReconnect["window"] -= ReportFromPage;
            Rendered.OnRendered -= NotifyEmptied;
            Rendered.OnEmptied -= NotifyEmptied;
            if (Module != null)
            {
                await Module.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }
    }
}
