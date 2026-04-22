
#if !BROWSER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Maui.Controls;
#else
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
#endif

using Microsoft.AspNetCore.SignalR.Client;

namespace Hosting.Services;



public abstract partial class BaseCircuitProvider(
    ICompositeProvider service,
    HttpClient? http = null,
    HubConnection? connection = null
)
{

    public static string HubAddress { get; } = "/api/hub";

    public event Action<bool, ConnectionMetadata>? OnConnectionDown;
    public event Action<bool, ConnectionMetadata>? OnConnectionUp;

    public int DefaultTTL { get; set; } = 100;
    protected static readonly ConcurrentDictionary<string, ConnectionMetadata> _activeCircuits = new();

    public ICompositeProvider Service { get; } = service;
    public HttpClient? Http { get; } = http;

    protected HubConnection? _connection = connection;
    private HubConnection Connection
    {
        get
        {
            if (_connection == null) throw new InvalidOperationException("Check if hub is available first.");
            return _connection;
        }
        set => _connection = value;
    }

    public abstract bool IsConnected { get; }

    public abstract int ClientCount { get; }

    public abstract bool IsAppConnected { get; }

    public abstract bool IsServerConnected { get; }

    public abstract bool IsSignalCircuit { get; }

    public abstract bool IsHubConnected { get; }

    public virtual async Task OnConnectionUpAsync(ConnectionMetadata metadata)
    {
        // Add or update the circuit in the static dictionary
        _activeCircuits.TryAdd(metadata.Id, metadata);

        OnConnectionUp?.Invoke(true, metadata);
    }

    public virtual async Task OnConnectionDownAsync(ConnectionMetadata metadata)
    {
        // Remove the circuit from the static dictionary
        _activeCircuits.TryRemove(metadata.Id, out _);

        OnConnectionDown?.Invoke(false, metadata);
    }

    public abstract Task<T?> InvokeAsync<T>(string? method, CancellationToken? ct = null);

    public abstract Task<T?> InvokeAsync<T>(string? method, object?[]? parameters);
}

#if BROWSER
// TODO: make webassembly the page serving server
public partial class CircuitProvider : BaseCircuitProvider, ICircuitProvider, IAsyncDisposable
{

    public override bool IsHubConnected => _connection?.State == HubConnectionState.Connected;
    public override bool IsSignalCircuit => IsHubConnected;
    public override bool IsAppConnected => true;
    public override bool IsServerConnected => IsConnected;
    public override int ClientCount => 1;


    public IRenderState Rendered { get; }
    public IPageEvents PageManager { get; }
    public NavigationManager Nav { get; }

    public override bool IsConnected => IsHubConnected;

    public Dictionary<string, string> RequestParameters => Nav.Uri.Query();


    public CircuitProvider(
        ICompositeProvider service, 
        NavigationManager nav, 
        IPageEvents page, 
        HttpClient http, 
        IRenderState rendered, 
        HubConnection? connection = null)
        :base(service, http, connection)
    {
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
            await OnConnectionUpAsync(new ConnectionMetadata(id ?? _connection.ConnectionId ?? "unknown", DateTime.UtcNow));

        _connection.Closed += async (ex) =>
            await OnConnectionDownAsync(new ConnectionMetadata(_connection.ConnectionId ?? "unknown", DateTime.UtcNow, ex?.Message, ex));
    }

    protected void ReportFromPage(string? state)
    {
        if (state == "hide")
        {
            _ = OnConnectionUpAsync(new ConnectionMetadata(_connection?.ConnectionId ?? "unknown", DateTime.UtcNow));
        }
        else
        {
            _ = OnConnectionDownAsync(new ConnectionMetadata(_connection?.ConnectionId ?? "unknown", DateTime.UtcNow, state));
        }
    }



    public async ValueTask DisposeAsync()
    {
        PageManager.OnReconnect -= ReportFromPage;
        GC.SuppressFinalize(this);
    }

    
}

#else

public partial class CircuitHub(ICircuitProvider Circuit) : Microsoft.AspNetCore.SignalR.Hub
{
    // This cannot be generic. It must be a concrete method name.
    public async Task<object?> Execute(string method, JsonElement parameters)
    {
        // Route this to your internal debounced logic
        if (parameters.ValueKind == JsonValueKind.Array)
            return await Circuit.InvokeAsync<object?>(method, [..parameters.ToArray().Cast<object?>()]);
        else
            return await Circuit.InvokeAsync<object?>(method, [parameters]);
    }
}


public partial class CircuitProvider(
    ICompositeProvider service,
    HttpClient? http = null,
    HubConnection? connection = null,
    Lazy<Application?>? app = null
) : BaseCircuitProvider(service, http, connection), ICircuitProvider, IAsyncDisposable
{

    public override bool IsSignalCircuit => true;

    public override bool IsConnected => !_activeCircuits.IsEmpty;

    public override bool IsHubConnected => !_activeCircuits.IsEmpty;

    public override bool IsAppConnected => App?.Value != null;

    public override bool IsServerConnected => App?.Value != null;

    public override int ClientCount => _activeCircuits.Count;

    public Lazy<Application?>? App { get; } = app;

    public async ValueTask DisposeAsync()
    {
        //Circuit.OnConnectionDown -= OnConnectionDown;
        //Circuit.OnConnectionUp -= OnConnectionUp;
        //base.Dispose();
        GC.SuppressFinalize(this);
    }

}


public abstract partial class BaseCircuitProvider : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, ICircuitProvider, IHasCircuit
{

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

public interface IHasCircuit
{

    Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct);

    Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct);
}


public static class HttpContextExtensions
{
    public static Dictionary<string, MethodInfo>? Services { get; private set; }

    public static bool IsSignalCircuit(this HttpContext? context)
    {
        if (context == null) return false;
        return context.Response.HasStarted
            && context.WebSockets.IsWebSocketRequest
            && (context.Request.Path.Value?.StartsWith("/_blazor") == true
            || context.Request.Path.Value?.StartsWith(CircuitProvider.HubAddress) == true);
    }


    public static bool IsSignalCircuit(this IHttpContextAccessor? accessor)
    {
        if (accessor == null) return false;
        return accessor.HttpContext?.IsSignalCircuit() == true;
    }

    public static void MapFullCircuits(this IEndpointRouteBuilder endpoints, IServiceCollection services)
    {
        try
        {
            endpoints.MapHub<CircuitHub>(CircuitProvider.HubAddress);

            var serviceable = services.Select(s => s.ServiceType);

            Services = serviceable
                .SelectMany(TypeExtensions.Routes)
                .ToDictionary(r => r.Route()!, r => r);

            foreach (var service in Services)
            {
                var routeBuilder = endpoints.Map(service.Key, CircuitProvider.OnExecuteAsync);

                //routeBuilder.RequireAuthorization();

                //routeBuilder.WithTags(service.Value.Name);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
#endif
