
namespace Hosting.Services;

public record ServiceWorkerStatus(
 bool Supported,
 bool IsActive,
 bool IsWaiting,
 bool IsInstalling,
 string? Scope,
 string? State);


public interface IServiceWorkerService : IHasModule
{
    Task<ServiceWorkerStatus> GetStatusAsync();
    Task<bool> RegisterAsync(string serviceName, string? displayName = null, string? appPath = null);

    // Lifecycle
    Task<bool> UnregisterAsync();

    // The "PostMessage" Parity
    Task<TResponse?> PostMessageAsync<TRequest, TResponse>(TRequest message, int timeoutMs = 10000);

    // Utilities for your version-sync logic
    Task<long?> GetVersionAsync();
    Task ForceSyncVersionAsync(string versionUrl);

    event Action<object?>? OnMessageReceived;
}
