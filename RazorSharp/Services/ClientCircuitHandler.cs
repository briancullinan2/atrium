using DataLayer.Utilities;
using FlashCard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Client.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.JSInterop;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebClient.Services
{
    public class CircuitHandler : IConnectionStateProvider, IAsyncDisposable
    {
        public event Action<bool, ConnectionMetadata>? OnConnectionDown;
        public event Action<bool, ConnectionMetadata>? OnConnectionUp;

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;
        public int ClientCount => 1;

        private IRenderStateProvider Rendered { get; }
        private Task ModuleInitialize { get => _restartRequired.Task; }

        protected void NotifyEmptied()
        {
            if (_restartRequired.Task.IsCompleted)
                _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        protected void NotifyRendered() => _ = EnsureModuleLoaded();


        private TaskCompletionSource<bool> _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);


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
                PageManager.OnReconnect += ReportFromPage;

            }
            finally
            {
                // 4. Always release the lock in a finally block
                _loadLock.Release();
            }
        }


        protected void ReportFromPage(string? state)
        {
            if (state == "hide")
            {
                OnConnectionUp?.Invoke(true, new ConnectionMetadata(_connection?.ConnectionId ?? "unknown", DateTime.UtcNow));
            }
            else
            {
                OnConnectionDown?.Invoke(false, new ConnectionMetadata(_connection?.ConnectionId ?? "unknown", DateTime.UtcNow, state));
            }
        }


        public IPageManager PageManager { get; }

        private readonly HubConnection? _connection;

        public CircuitHandler(IPageManager page, IRenderStateProvider rendered, HubConnection? connection = null)
        {
            Rendered = rendered;
            Rendered.OnEmptied += NotifyEmptied;
            Rendered.OnRendered += NotifyRendered;
            PageManager = page;

            _connection = connection;
            // become the connection
            /*
            if (connection == null)
            {
                _connection = new HubConnectionBuilder()
                .AddMessagePackProtocol()
                .WithUrl(Navigation.ToAbsoluteUri("/_blazor"), options =>
                {
                    // Ensure this isn't returning null!
                    options.AccessTokenProvider = () =>
                    {
                        //var token = GetSavedToken();
                        return Task.FromResult<string?>(""); // Use empty string, never null
                    };
                })
                .WithAutomaticReconnect([
                    TimeSpan.Zero,           // Immediate retry
                    TimeSpan.FromSeconds(2), // Arizona network latency buffer
                    TimeSpan.FromSeconds(10)
                ])
                .Build();
            }
            */
            if (_connection == null)
            {
                return;
            }

            // check passed in reference is null
            if (connection == null)
            {
                var reference = DotNetObjectReference.Create(this);
                _ = _connection.InvokeAsync("RegisterCircuit", reference);
                _ = _connection.StartAsync();
            }

            _connection.Reconnected += async (id) =>
                OnConnectionUp?.Invoke(true, new ConnectionMetadata(id ?? _connection.ConnectionId ?? "unknown", DateTime.UtcNow));

            _connection.Closed += async (ex) =>
                OnConnectionDown?.Invoke(false, new ConnectionMetadata(_connection.ConnectionId ?? "unknown", DateTime.UtcNow, ex?.Message, ex));
        }


        public async ValueTask DisposeAsync()
        {
            PageManager.OnReconnect -= ReportFromPage;
            Rendered.OnRendered -= NotifyEmptied;
            Rendered.OnEmptied -= NotifyEmptied;
            GC.SuppressFinalize(this);
        }
    }
}
