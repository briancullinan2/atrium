
#if !BROWSER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Maui.Controls;
#endif

namespace Hosting.Services;



public partial class CircuitProvider : ICircuitProvider
{

    public static string HubAddress { get; } = "/api/hub";

    public event Action<bool, ConnectionMetadata>? OnConnectionDown;
    public event Action<bool, ConnectionMetadata>? OnConnectionUp;

    public int DefaultTTL { get; set; } = 100;
    private static readonly ConcurrentDictionary<string, ConnectionMetadata> _activeCircuits = new();

    public IServiceProvider Service { get; }
    public HttpClient? Http { get; }

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
// TODO: make webassembly the page serving server
public partial  class CircuitProvider : IAsyncDisposable
{

    public bool IsHubConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsSignalCircuit => IsHubConnected;
    public bool IsAppConnected => true;
    public bool IsServerConnected => IsConnected;
    public int ClientCount => 1;


    public IRenderState Rendered { get; }
    public IPageManager PageManager { get; }
    public NavigationManager Nav { get; }

    public bool IsConnected => IsHubConnected;

    public Dictionary<string, string> RequestParameters => Nav.Uri.Query();


    public CircuitProvider(
        IServiceProvider service, 
        NavigationManager nav, 
        IPageManager page, 
        HttpClient http, 
        IRenderState rendered, 
        HubConnection? connection = null)
    {
        Service = service;
        Http = http;
        Rendered = rendered;
        PageManager = page;
        Nav = nav;
        _connection = connection;
        // become the connection
        /*
        if (connection == null)
        {
            _connection = new HubConnectionBuilder()
            .AddMessagePackProtocol()
            .WithUrl(Navigation.ToAbsoluteUri(HubAddress), options =>
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



    public async ValueTask DisposeAsync()
    {
        PageManager.OnReconnect -= ReportFromPage;
        GC.SuppressFinalize(this);
    }

    
}

#else

public partial class CircuitProvider : Microsoft.AspNetCore.SignalR.Hub, IAsyncDisposable
{

    public bool IsSignalCircuit => true;

    public bool IsConnected => !_activeCircuits.IsEmpty;

    public bool IsHubConnected => !_activeCircuits.IsEmpty;

    public bool IsAppConnected => App?.Value != null;

    public bool IsServerConnected => App?.Value != null;

    public int ClientCount => _activeCircuits.Count;

    public CircuitHandler Circuit { get; }

    public Lazy<Application?>? App { get; }

    public CircuitProvider(
        IServiceProvider service, 
        CircuitHandler circuit, 
        Lazy<Microsoft.Maui.Controls.Application?>? app = null, 
        HttpClient? http = null,
        HubConnection? connection = null)
    {
        _connection = connection;
        App = app;
        Service = service;
        Http = http;
        Circuit = circuit;
        Circuit.OnConnectionDown += OnConnectionDown;
        Circuit.OnConnectionUp += OnConnectionUp;
    }

    public async ValueTask DisposeAsync()
    {
        Circuit.OnConnectionDown -= OnConnectionDown;
        Circuit.OnConnectionUp -= OnConnectionUp;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}


public class CircuitHandler() 
    : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler
{
    public event Action<bool, ConnectionMetadata>? OnConnectionDown;
    public event Action<bool, ConnectionMetadata>? OnConnectionUp;

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
    {
        OnConnectionUp?.Invoke(true, new ConnectionMetadata(circuit.Id, DateTime.UtcNow));
        await base.OnConnectionUpAsync(circuit, ct);
    }

    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
    {
        OnConnectionDown?.Invoke(true, new ConnectionMetadata(circuit.Id, DateTime.UtcNow, "Circuit Disconnected"));
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
            && (context.Request.Path.StartsWithSegments("/_blazor")
            || context.Request.Path.StartsWithSegments(CircuitProvider.HubAddress));
    }


    public static bool IsSignalCircuit(this IHttpContextAccessor? accessor)
    {
        if (accessor == null) return false;
        return accessor.HttpContext?.IsSignalCircuit() == true;
    }

    public static void MapFullCircuits(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<CircuitProvider>(CircuitProvider.HubAddress);

        var services = Assembly.GetCallingAssembly().GetAssemblies().ToServices();

        foreach (var service in services)
        {
            var routes = service.Routes();
            if (routes == null || routes.Count == 0) continue;

            foreach(var method in routes)
            {
                var route = method.Route();
                if (route == null) continue;

                var routeBuilder = endpoints.MapPost(route, CircuitProvider.OnExecuteAsync);

                routeBuilder.RequireAuthorization();

                routeBuilder.WithTags(method.Name);
            }

        }
    }


}
#endif
