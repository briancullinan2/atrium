using System.Net.Http;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using RazorSharp.Services;
using Microsoft.AspNetCore.SignalR;
using TaskExtensions = Extensions.PrometheusTypes.TaskExtensions;

namespace Extensions.SlenderServices
{
    public interface IFullCircuit
    {
        string Name { get; }
        Task<object?> ExecuteAsync();
        MethodInfo ProviderMethod { get; }
        Type? ProviderType { get; }
        string Path { get; }
    }


    // TODO: this is confusing because i want to insert a parameter but i also want the web service to automatically inject them

    [RequiresUnreferencedCode("serialized the types in this class to transfer between HttpContext and WebAssembly")]
    public class FullCircuit<T>(ICircuitProvider Circuit, HttpClient? Http = null) : Hub
    {
        // Caching/TTL Logic
        private T? _cachedValue;
        private DateTime _lastFetched = DateTime.MinValue;

        public int DefaultTTL { get; set; } = 100;

        // The Plugs (Sources)
        private Func<Task<T>>? StoredProvider = null;
        public Func<Task<T>> LocalProvider
        {
            get
            {
                if (StoredProvider == null)
                    throw new InvalidOperationException("Must set provider method to a class method.");
                return StoredProvider;
            }
            set => StoredProvider = value;
        }

        [JsonIgnore]
        public Type? ProviderType { get => LocalProvider.Method.DeclaringType; }
        [JsonIgnore]
        public MethodInfo ProviderMethod { get => LocalProvider.Method; }
        private string? StoredName = null;
        public string Name { get => StoredName ?? ProviderMethod.Name; set => StoredName = value; }
        public string Path { 
            get
            {
                var type = ProviderType;
                var ns = (type?.Namespace ?? "Global").Split('.').ToList();
                if (!string.IsNullOrWhiteSpace(type?.Name))
                    ns.Add(type.Name);
                string url = $"/api/{string.Join('/', ns)}/{Name}";
                return url;
            }
        }
        public T? StaticData { get; set; }

        public async Task<T?> ExecuteAsync()
        {
            return await TaskExtensions.Debounce(ExecuteAsyncDebounced, DefaultTTL);
        }


        public async Task<T?> ExecuteAsyncDebounced(
            // TODO: get force out of somewhere
            /* bool force = false */)
        {
            if (_cachedValue != null && DateTime.Now < _lastFetched + TimeSpan.FromMilliseconds(DefaultTTL))
            {
                return _cachedValue;
            }

            T? result = default;

            if (Http == null && OperatingSystem.IsBrowser() && Circuit.IsSignalCircuit)
            {
                if (StaticData != null) result = StaticData;
                if (LocalProvider != null) result = await LocalProvider();
                else throw new InvalidOperationException("Nothing to do in: " + GetType());
            }
            else if (StoredName != null && Circuit.IsSignalCircuit)
            {
                result = await Circuit.InvokeAsync<T>(nameof(ExecuteAsync));
            }
            else if (Http != null)
            {
                result = await Http.GetFromJsonAsync<T>(Path);
            }
            else
                throw new InvalidOperationException("Couldn't resolve invocation: " + JsonSerializer.Serialize(this));

            if (result != null)
            {
                _cachedValue = result;
                _lastFetched = DateTime.Now;
            }

            return result;
        }

    }


    [RequiresUnreferencedCode("serialized the types in this class to transfer between HttpContext and WebAssembly")]
    public class FullCircuit(ICircuitProvider Circuit, HttpClient? Http = null) : FullCircuit<object?>(Circuit, Http), IFullCircuit
    {
    }

}
