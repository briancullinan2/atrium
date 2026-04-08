#if WINDOWS
using Extensions.PlayfulPlatforms.Windows;
using Hosting.Services;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Pipes;

namespace Hosting.Platforms.Windows;

public class WindowsServiceWorkerService : IServiceWorkerService
{
    public const string ProtocolScheme = "atrium";
    private const string PipeName = "Atrium_ServiceWorker_Pipe";
    private string? _currentScheme;

    public Task ModuleInitialize => _renderTcs.Task;

    private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsReady => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

    public event Action<object?>? OnMessageReceived;

    public async Task InitializeAsync()
    {
        // Windows-specific init logic (e.g., verifying background process is running)
        //if (_renderTcs.Task.IsCompleted)
        //    _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // TODO: only once
        //_renderTcs.TrySetResult(true);

        await Task.CompletedTask;
    }

    public async Task<ServiceWorkerStatus> GetStatusAsync()
    {
        bool pipeExists = System.IO.Directory.GetFiles(@"\\.\pipe\").Any(f => f.Contains(PipeName));
        return new ServiceWorkerStatus(
            Supported: true,
            IsActive: pipeExists,
            IsWaiting: false,
            IsInstalling: false,
            Scope: _currentScheme,
            State: pipeExists ? "activated" : "redundant"
        );
    }



    public static async Task<bool> RegisterSCM(string serviceName, string displayName, string? appPath = null)
    {
        try
        {
            string targetPath = appPath ?? Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(targetPath)) return false;

            // "sc create" is the standard Windows utility for service registration
            // binPath must be quoted if it contains spaces
            string arguments = $"create \"{serviceName}\" binPath= \"\\\"{targetPath}\\\" --service\" start= auto DisplayName= \"{displayName}\"";

            ProcessStartInfo startInfo = new()
            {
                FileName = "sc.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using Process? process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register service: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> RegisterNativeAsync(string serviceName, string displayName, string? appPath = null)
    {
        const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
        const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        const uint SERVICE_AUTO_START = 0x00000002;
        const uint SERVICE_ERROR_NORMAL = 0x00000001;
        const uint SERVICE_ALL_ACCESS = 0xF01FF;

        IntPtr scmHandle = AdvApi.OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
        if (scmHandle == IntPtr.Zero) return false;

        try
        {
            string targetPath = appPath ?? Environment.ProcessPath ?? "";
            IntPtr svcHandle = AdvApi.CreateService(
                scmHandle, serviceName, displayName,
                SERVICE_ALL_ACCESS, SERVICE_WIN32_OWN_PROCESS,
                SERVICE_AUTO_START, SERVICE_ERROR_NORMAL,
                $"\"{targetPath}\" --service", null, IntPtr.Zero, null, null, null);
            if (svcHandle == IntPtr.Zero) return false;

            AdvApi.CloseServiceHandle(svcHandle);
            return true;
        }
        finally
        {
            AdvApi.CloseServiceHandle(scmHandle);
        }
    }


    public async Task<bool> RegisterAsync(string serviceName, string? displayName = null, string? appPath = null)
    {
        displayName ??= serviceName;
        return await RegisterNativeAsync(serviceName, displayName, appPath);
    }

    public async Task<bool> UnregisterAsync()
    {
        // Logic to remove Registry keys or kill background process
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Parity with browser postMessage using Named Pipes.
    /// </summary>
    public async Task<TResponse?> PostMessageAsync<TRequest, TResponse>(TRequest message, int timeoutMs = 10000)
    {
        using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        
        try
        {
            await pipeClient.ConnectAsync(timeoutMs);
            
            // 1. Serialize and Send
            string jsonRequest = JsonSerializer.Serialize(message);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonRequest);
            await pipeClient.WriteAsync(buffer);

            // 2. Await Response (The "MessageChannel" return)
            byte[] responseBuffer = new byte[65536]; // 64KB limit
            int bytesRead = await pipeClient.ReadAsync(responseBuffer);
            
            if (bytesRead == 0) return default;
            
            string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
            var result = JsonSerializer.Deserialize<TResponse>(jsonResponse);
            OnMessageReceived?.Invoke(result);
            return result;
        }
        catch { return default; }
    }

    public async Task<long?> GetVersionAsync()
    {
        // Ping the worker for its version via the pipe
        var response = await PostMessageAsync<object, long[]>(new { type = "GET_VERSION" });
        return response?[1];
    }

    public async Task ForceSyncVersionAsync(string versionUrl)
    {
        // Implementation matches your logic: compare local assembly/process version vs server
        await Task.CompletedTask; 
    }

    public async Task<bool> RegisterProtocolHandlerAsync(string? scheme = ProtocolScheme, string? appPath = null)
    {
        if (scheme == null) throw new InvalidOperationException("Scheme cannot be null for Windows protocol handler registration.");
        // Default to the current executable path if not provided
        appPath ??= (System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? throw new InvalidOperationException("Unable to determine application path for protocol handler registration.");
        try
        {
            // On Windows, 'Register' implies setting up the protocol handler
            _currentScheme = scheme;
            using var key = Registry.ClassesRoot.CreateSubKey(scheme);
            key.SetValue("", $"URL:{scheme} Protocol");
            key.SetValue("URL Protocol", "");

            using var shell = key.CreateSubKey(@"shell\open\command");
            // "%1" ensures the full app:// URL is passed as the first argument to Atrium
            shell.SetValue("", $"\"{appPath}\" \"%1\"");


            return true;
        }
        catch (Exception) { return false; }
    }
}
#endif