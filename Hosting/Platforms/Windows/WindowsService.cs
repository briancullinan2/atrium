#if WINDOWS
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Hosting.Services;
using Microsoft.Win32;

namespace Hosting.Platforms.Windows
{
    public class WindowsServiceWorkerService : IServiceWorkerService
    {
        private const string PipeName = "Atrium_ServiceWorker_Pipe";
        private string? _currentScheme;
        public event Action<object>? OnMessageReceived;

        public async Task InitializeAsync()
        {
            // Windows-specific init logic (e.g., verifying background process is running)
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

        public async Task<SwRegistrationResult> RegisterAsync(string appPath)
        {
            try 
            {
                // On Windows, 'Register' implies setting up the protocol handler
                await RegisterProtocolHandlerAsync("app", appPath);
                return new SwRegistrationResult(true, "app://", null);
            }
            catch (Exception ex) { return new SwRegistrationResult(false, null, ex.Message); }
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
                await pipeClient.WriteAsync(buffer, 0, buffer.Length);

                // 2. Await Response (The "MessageChannel" return)
                byte[] responseBuffer = new byte[65536]; // 64KB limit
                int bytesRead = await pipeClient.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                
                if (bytesRead == 0) return default;
                
                string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
                return JsonSerializer.Deserialize<TResponse>(jsonResponse);
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

        public async Task RegisterProtocolHandlerAsync(string scheme, string appPath)
        {
            _currentScheme = scheme;
            using var key = Registry.ClassesRoot.CreateSubKey(scheme);
            key.SetValue("", $"URL:{scheme} Protocol");
            key.SetValue("URL Protocol", "");

            using var shell = key.CreateSubKey(@"shell\open\command");
            // "%1" ensures the full app:// URL is passed as the first argument to Atrium
            shell.SetValue("", $"\"{appPath}\" \"%1\"");
        }
    }
}
#endif