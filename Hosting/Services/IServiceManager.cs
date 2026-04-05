using System;
using System.Collections.Generic;
using System.Text;

namespace Hosting.Services
{
    public record ServiceWorkerStatus(
     bool Supported,
     bool IsActive,
     bool IsWaiting,
     bool IsInstalling,
     string? Scope,
     string? State);

    public record SwRegistrationResult(bool Success, string? Scope, string? Error);

    public interface IServiceWorkerService
    {
        Task InitializeAsync();
        Task<ServiceWorkerStatus> GetStatusAsync();
        Task<SwRegistrationResult> RegisterAsync(string scriptUrl);

        // Lifecycle
        Task<bool> UnregisterAsync();

        // The "PostMessage" Parity
        Task<TResponse?> PostMessageAsync<TRequest, TResponse>(TRequest message, int timeoutMs = 10000);

        // Utilities for your version-sync logic
        Task<long?> GetVersionAsync();
        Task ForceSyncVersionAsync(string versionUrl);

        event Action<object>? OnMessageReceived;
    


        // Protocol Handling
        Task RegisterProtocolHandlerAsync(string scheme, string appPath);
    }
}
namespace Hosting.Services
{
    public record ServiceWorkerStatus(
        bool Supported,
        bool IsActive,
        bool IsWaiting,
        bool IsInstalling,
        string? Scope,
        string? State);

    public record SwRegistrationResult(bool Success, string? Scope, string? Error);

    public interface IServiceWorkerService
    {
        Task InitializeAsync();
        Task<ServiceWorkerStatus> GetStatusAsync();
        Task<SwRegistrationResult> RegisterAsync(string scriptUrl);
        Task<bool> UnregisterAsync();

        // The "PostMessage" Parity
        Task<TResponse?> PostMessageAsync<TRequest, TResponse>(TRequest message, int timeoutMs = 10000);

        // Utilities
        Task<long?> GetVersionAsync();
        Task ForceSyncVersionAsync(string versionUrl);
        Task RegisterProtocolHandlerAsync(string scheme, string appPath);

        event Action<object>? OnMessageReceived;
    }
}