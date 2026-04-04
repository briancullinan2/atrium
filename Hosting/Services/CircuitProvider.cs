
#if BROWSER
using Microsoft.AspNetCore.SignalR.Client;
#else
using Microsoft.AspNetCore.Components.Server.Circuits;
#endif
using System.Collections.Concurrent;

namespace Hosting.Services
{


    public partial class CircuitProvider : ICircuitProvider
    {
        private static readonly ConcurrentDictionary<string, ConnectionMetadata> _activeCircuits = new();

        public event Action<bool, ConnectionMetadata>? OnConnectionDown;
        public event Action<bool, ConnectionMetadata>? OnConnectionUp;

        public bool IsConnected => OperatingSystem.IsBrowser() || IsSignalCircuit
            ? IsHubConnected
            : !_activeCircuits.IsEmpty;
        public int ClientCount => OperatingSystem.IsBrowser() ? 1 : _activeCircuits.Count + (IsAppConnected ? 1 : 0);


        public async Task OnConnectionUpAsync(ConnectionMetadata metadata, CancellationToken ct)
        {
            // Add or update the circuit in the static dictionary
            _activeCircuits.TryAdd(metadata.Id, metadata);

            OnConnectionUp?.Invoke(true, metadata);
        }

        public async Task OnConnectionDownAsync(ConnectionMetadata metadata, CancellationToken ct)
        {
            // Remove the circuit from the static dictionary
            _activeCircuits.TryRemove(metadata.Id, out _);

            OnConnectionDown?.Invoke(false, metadata);
        }

    }

#if BROWSER
    public partial  class CircuitProvider : IAsyncDisposable
    {
    
        public bool IsHubConnected => _connection?.State == HubConnectionState.Connected;
        public bool IsSignalCircuit => IsHubConnected;
        public bool IsAppConnected => true;

        public IRenderState Rendered { get; }
        public IPageManager PageManager { get; }

        private readonly HubConnection? _connection;

        public CircuitHandler(IPageManager page, IRenderState rendered, HubConnection? _connection = null)
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

        
        protected void ReportFromPage(string? state)
        {
            if (state == "hide")
            {
                base.OnConnectionUpAsync(new ConnectionMetadata(_connection?.ConnectionId ?? "unknown", DateTime.UtcNow));
            }
            else
            {
                OnConnectionDown?.Invoke(false, new ConnectionMetadata(_connection?.ConnectionId ?? "unknown", DateTime.UtcNow, state));
            }
        }


        public async ValueTask DisposeAsync()
        {
            PageManager.OnReconnect -= ReportFromPage;
            Rendered.OnRendered -= NotifyEmptied;
            Rendered.OnEmptied -= NotifyEmptied;
            GC.SuppressFinalize(this);
        }
    }

#else
    public partial class CircuitProvider(Lazy<MauiApp?>? App = null, HttpContext? Context = null) 
        : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, ICircuitProvider
    {
        public bool IsSignalCircuit => Context.IsSignalCircuit();

        public bool IsHubConnected => Context != null && _activeCircuits.ContainsKey(Context.Connection.Id);

        public bool IsAppConnected => App?.Value.HasValue == true;

        public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
        {
            await OnConnectionUpAsync(new ConnectionMetadata(circuit.Id, DateTime.UtcNow), ct);
            await base.OnConnectionUpAsync(circuit, ct);
        }

        public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
        {
            await OnConnectionDownAsync(new ConnectionMetadata(circuit.Id, DateTime.UtcNow, "Circuit Disconnected"), ct);
            await base.OnConnectionDownAsync(circuit, ct);
        }
    }

#endif


    public static class HttpContextExtensions
    {
        public static bool IsSignalCircuit(this HttpContext? context)
        {
            if (context == null) return false;
            return context.Response.HasStarted
                && context.WebSockets.IsWebSocketRequest
                && context.Request.Path.StartsWithSegments("/_blazor");
        }


        public static bool IsSignalCircuit(this IHttpContextAccessor? accessor)
        {
            if (accessor == null) return false;
            return accessor.HttpContext?.IsSignalCircuit() == true;
        }

    }


}
