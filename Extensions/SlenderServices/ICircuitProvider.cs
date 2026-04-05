using System.Collections.Concurrent;

namespace RazorSharp.Services
{


    public record ConnectionMetadata(
        string Id,
        DateTime Timestamp,
        string? Reason = null,
        Exception? Exception = null
    );

    public interface ICircuitProvider
    {
        event Action<bool, ConnectionMetadata>? OnConnectionDown;
        event Action<bool, ConnectionMetadata>? OnConnectionUp;

        bool IsConnected { get; }
        int ClientCount { get; }
        bool IsAppConnected { get; }
        bool IsSignalCircuit { get; }
        bool IsHubConnected { get; }

        // Standardized reporting methods
        Task OnConnectionUpAsync(ConnectionMetadata metadata);
        Task OnConnectionDownAsync(ConnectionMetadata metadata);

        Task<T> InvokeAsync<T>(string method, CancellationToken? ct = null); // TODO: params object?[]? args);
    }

}
