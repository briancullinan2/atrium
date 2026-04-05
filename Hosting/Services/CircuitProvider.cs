

#if BROWSER
using Microsoft.AspNetCore.SignalR.Client;
#else
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
#endif

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

        public int DefaultTTL { get; set; } = 100;

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

        public async Task<TResult?> InvokeAsync<TResult>(string method, params object?[]? parameters)
        {
            return await TaskExtensions.Debounce(ExecuteAsyncDebounced<TResult>, DefaultTTL, method, parameters);
        }


        public async Task<TResult?> RespondCircuit<TResult>(MemberInfo? methodInfo, params object?[]? parameters)
        {
            if(methodInfo == null || methodInfo.DeclaringType == null)
                throw new InvalidOperationException("Couldn't find service provider: " + methodInfo);
            if (!IsSignalCircuit)
                throw new InvalidOperationException("Not a signal circuit: " + methodInfo);

            var Implementation = Service.GetRequiredService(methodInfo.DeclaringType);
            object? result;
            if (methodInfo is FieldInfo field)
            {
                result = field.GetValue(Implementation) is TResult;
            }
            if (methodInfo is PropertyInfo property)
            {
                result = parameters?.Length > 0 ? property.GetValue(Implementation, parameters) : property.GetValue(Implementation);
            }
            if (methodInfo is MethodInfo runable)
            {
                // TODO: put together a list of parameters and services
                result = runable.Invoke(Implementation, parameters);
            }
            else throw new InvalidOperationException("Nothing to do in: " + methodInfo);

            if (result == null || result.GetType().Extends(typeof(TResult)))
                return (TResult?)result;
            else throw new InvalidOperationException("Result did not type cast from: "
                + result.GetType().AssemblyQualifiedName
                + " to " + typeof(TResult).AssemblyQualifiedName);

        }


        public async Task<TResult?> RespondRemote<TResult>(MemberInfo methodInfo)
        {
            if (Http == null)
                throw new InvalidOperationException("Http client not available");

            if (methodInfo is MethodInfo runable)
            {
                var serviceTypes = runable.GetParameters().ToServices(Service);
                // TODO: dont serialize services, they will get rebuilt on the ends
            }
            // TODO: add fun serialization
            result = await Http.GetFromJsonAsync<TResult>(Path);
        }


        public async Task<TResult?> ExecuteAsyncDebounced<TImplementation, TResult>(
            string? method,
            object?[]? parameters
            // TODO: get force out of somewhere
            /* bool force = false */)
        {
            // TODO: the same Debounce and QueryNow does with parameters

            //if (_cachedValue != null && DateTime.Now < _lastFetched + TimeSpan.FromMilliseconds(DefaultTTL))
            //{
            //    return _cachedValue;
            //}

            var type = typeof(TImplementation);
            MemberInfo? methodInfo = type.GetMethods(method).FirstOrDefault() as MemberInfo
                ?? type.GetProperties(method).FirstOrDefault() as MemberInfo
                ?? type.GetFields(method).FirstOrDefault() as MemberInfo;

            if (methodInfo == null || !methodInfo.IsRoutable())
                throw new InvalidOperationException("Tried to invoke unroutable method: " + method + " on " + type.AssemblyQualifiedName);
        }

        public static async Task<TResult?> OnExecuteAsync<TResult>(ICircuitProvider service, string method, params object?[]? parameters)
        {
            return await TaskExtensions.Debounce(service.ExecuteAsyncDebounced<TResult>, service.DefaultTTL, method, parameters);
        }
    }

#if BROWSER
    // TODO: make webassembly the page serving server
    public partial  class CircuitProvider : IAsyncDisposable
    {
    
        public bool IsHubConnected => _connection?.State == HubConnectionState.Connected;
        public bool IsSignalCircuit => IsHubConnected;
        public bool IsAppConnected => true;

        public IRenderState Rendered { get; }
        public IPageManager PageManager { get; }
        public NavigationManager Nav { get; }
        public HttpClient Http { get; }
        public IServiceProvider Service { get; }

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


        public Dictionary<string, string> RequestParameters => Nav.Uri.Query();


        public CircuitProvider(IServiceProvider service, NavigationManager nav, IPageManager page, HttpClient http, IRenderState rendered, HubConnection? connection = null)
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


        public async Task<T?> InvokeAsync<T>(string method, CancellationToken? ct = null) => await Connection.InvokeAsync<T>(method, ct);
        public async Task<T?> InvokeAsync<T>(string method, object?[]? parameters) => parameters?.Length switch {
            1 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0)),
            2 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1)),
            3 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2)),
            4 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2), parameters.ElementAt(3)),
            5 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2), parameters.ElementAt(3), parameters.ElementAt(4)),
            _ => await Connection.InvokeAsync<T>(method, new CancellationTokenSource().Token)
            };
            


        public async ValueTask DisposeAsync()
        {
            PageManager.OnReconnect -= ReportFromPage;
            GC.SuppressFinalize(this);
        }

        
    }

#else
    public partial class CircuitProvider(IServiceProvider Service, Lazy<MauiApp?>? App = null, HttpClient? Http = null) 
        : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, ICircuitProvider
    {
        public bool IsSignalCircuit => true;

        public bool IsHubConnected => !_activeCircuits.IsEmpty;

        public bool IsAppConnected => App?.Value.HasValue == true;

        public Dictionary<string, string> RequestParameters => throw new NotImplementedException();

        
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


            foreach (var circuit in circuits)
            {
                if (circuit.Path == null) continue;

                endpoints.MapHub<FullCircuit<HostingService>>(circuit.Path);
                var routeBuilder = endpoints.MapPost(circuit.Path, circuit.OnExecuteAsync);

                routeBuilder.RequireAuthorization();

                routeBuilder.WithTags(circuit.Name);
            }
        }
    }
#endif

}
