
#if BROWSER
using Microsoft.AspNetCore.SignalR.Client;
#else
using Extensions.SlenderServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Routing;

#endif
using System.Collections.Concurrent;
using System.Reflection;

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


        public async Task OnConnectionUpAsync(ConnectionMetadata metadata)
        {
            // Add or update the circuit in the static dictionary
            _activeCircuits.TryAdd(metadata.Id, metadata);

            OnConnectionUp?.Invoke(true, metadata);
        }

        public async Task OnConnectionDownAsync(ConnectionMetadata metadata)
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


        private HubConnection? _connection;
        private HubConnection Connection
        {
            get
            {
                if (_connection == null) throw new InvalidOperationException("Check if hub is available first.");
                return _connection;
            }
            set => _connection = value;
        }

        public CircuitProvider(IPageManager page, IRenderState rendered, HubConnection? connection = null)
        {
            Rendered = rendered;
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
                _ = OnConnectionUpAsync(new ConnectionMetadata(Connection.ConnectionId ?? "unknown", DateTime.UtcNow));
            }
            else
            {
                _ = OnConnectionDownAsync(new ConnectionMetadata(Connection.ConnectionId ?? "unknown", DateTime.UtcNow, state));
            }
        }

        public async Task<T> InvokeAsync<T>(string method, CancellationToken? ct = null) => await Connection.InvokeAsync<T>(method, ct);

        public async ValueTask DisposeAsync()
        {
            PageManager.OnReconnect -= ReportFromPage;
            GC.SuppressFinalize(this);
        }
    }

#else
    public partial class CircuitProvider(Lazy<MauiApp?>? App = null, HttpContext? Context = null) 
        : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, ICircuitProvider
    {
        public bool IsSignalCircuit => Context.IsSignalCircuit();

        public bool IsHubConnected => Context != null && !_activeCircuits.IsEmpty;

        public bool IsAppConnected => App?.Value.HasValue == true;

        public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
        {
            await OnConnectionUpAsync(new ConnectionMetadata(circuit.Id, DateTime.UtcNow));
            await base.OnConnectionUpAsync(circuit, ct);
        }

        public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
        {
            await OnConnectionDownAsync(new ConnectionMetadata(circuit.Id, DateTime.UtcNow, "Circuit Disconnected"));
            await base.OnConnectionDownAsync(circuit, ct);
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

        public static void MapFullCircuits(this IEndpointRouteBuilder endpoints)
        {
            // 1. Get all registered circuits from the DI container
            var circuits = endpoints.ServiceProvider.GetServices<IFullCircuit>();

            foreach (var circuit in circuits)
            {
                // 2. Automagically create an API endpoint: /api/{name}
                //;
                //endpoints.MapPost($"/api/${circuit.Name}", circuit.ExecuteAsync);
                //endpoints.MapHub<FullCircuit>(circuit.Path);
                var routeBuilder = endpoints.MapPost(circuit.Path, circuit.ExecuteAsync);

                // Security Check
                bool hasAnonymous = circuit.ProviderMethod.GetCustomAttribute<AllowAnonymousAttribute>() != null
                                    || circuit.ProviderType?.GetCustomAttribute<AllowAnonymousAttribute>() != null;
                bool hasAuthorize = circuit.ProviderMethod.GetCustomAttribute<AuthorizeAttribute>() != null
                                    || circuit.ProviderType?.GetCustomAttribute<AuthorizeAttribute>() != null;

                if (hasAuthorize)
                {
                    routeBuilder.RequireAuthorization();
                }

                routeBuilder.WithTags(circuit.Name);
            }
        }
    }
#endif

}
