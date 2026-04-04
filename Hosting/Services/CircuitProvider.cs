
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Concurrent;

namespace Hosting.Services
{


    public abstract class BaseCircuitProvider : ICircuitProvider
    {
        private static readonly ConcurrentDictionary<string, ConnectionMetadata> _activeCircuits = new();

        public event Action<bool, ConnectionMetadata>? OnConnectionDown;
        public event Action<bool, ConnectionMetadata>? OnConnectionUp;

        public abstract bool IsSignalCircuit { get; }
        public abstract bool IsHubConnected { get; }
        public abstract bool IsAppConnected { get; }

        public bool IsConnected => OperatingSystem.IsBrowser() || IsSignalCircuit
            ? IsHubConnected
            : _activeCircuits.Any();
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


    internal class CircuitProvider(
#if !BROWSER
        Lazy<MauiApp?>? App = null,
#endif
        HttpContext? Context = null
#if BROWSER
        , HubConnection? connection = null
#endif
    )
#if !BROWSER
        : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, ICircuitProvider, IAsyncDisposable
#else
        : RazorSharp.Services.BaseCircuitProvider,  ICircuitProvider
#endif
    {
#if !BROWSER
        public bool IsSignalCircuit => Context.IsSignalCircuit();
#else
        public override bool IsSignalCircuit => Context.IsSignalCircuit();
#endif

#if !BROWSER
        public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
        {
            var data = new ConnectionMetadata(circuit.Id, DateTime.UtcNow);

            // Add or update the circuit in the static dictionary
            _activeCircuits.TryAdd(circuit.Id, data);

            IsConnected = true;
            OnConnectionUp?.Invoke(true, data);
            await base.OnConnectionUpAsync(circuit, ct);
        }

        public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
        {
            var data = new ConnectionMetadata(circuit.Id, DateTime.UtcNow, "Circuit Disconnected");

            // Remove the circuit from the static dictionary
            _activeCircuits.TryRemove(circuit.Id, out _);

            IsConnected = false;
            OnConnectionDown?.Invoke(false, data);
            await base.OnConnectionDownAsync(circuit, ct);
        }
#endif


        private IRenderState Rendered { get; }



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


        public IPageManager PageManager { get; }

        public override bool IsSignalCircuit => throw new NotImplementedException();

        public override bool IsHubConnected => _connection?.State == HubState;

        public override bool IsAppConnected => throw new NotImplementedException();

        private readonly HubConnection? _connection;

        public CircuitHandler(IPageManager page, IRenderState rendered, )
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
