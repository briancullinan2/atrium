using Microsoft.AspNetCore.Http;
using FlashCard.Services;
using System.Net.Http.Json;
using DataLayer.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Reflection;



#if WINDOWS
using System.ServiceProcess;
#endif
using System.Text.Json;

namespace Atrium.Services
{
    internal class HostingService : IHostingService
    {
        private static readonly string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static HostingSettings? Settings { get; set; }
        public static Task<bool?>? Working { get; set; }

        static HostingService()
        {
            var savedSettings = Path.Combine(homeDirectory, ".credentials", "atrium-hosting.json");
            if (File.Exists(savedSettings))
            {
                try
                {
                    Settings = JsonSerializer.Deserialize<HostingSettings>(File.ReadAllText(savedSettings));
                }
                catch (Exception) { }
            }
        }

        private static HttpClient? _httpClient;
        public HostingService(HttpClient client)
        {
            _httpClient ??= client;
            Working ??= IsWorking();
        }

        public async Task<bool?> CheckInstalled()
        {
            return CheckServiceInstalled();
        }

        public static bool CheckServiceInstalled()
        {
#if WINDOWS
            const string serviceName = "Cloudflared";
            // Get all installed services and check for a match
            return ServiceController.GetServices()
                .Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
#else
            return false;
#endif
        }


#if WINDOWS
        private static readonly long _appStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Cached after the first check
        private static long? _latestAssemblyTime;

        public static async Task OnVersionCheck(HttpContext context)
        {
            if (!_latestAssemblyTime.HasValue)
            {

                // Find the newest 'Last Write Time' across all loaded assemblies.
                // This captures the main app + any referenced DLLs that were updated.
                _latestAssemblyTime = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                    .Select(a => {
                        try { return new FileInfo(a.Location).LastWriteTimeUtc; }
                        catch { return DateTime.MinValue; }
                    })
                    .ToList()
                    .Max()
                    .ToUniversalTime()
                    .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    .Ticks / 10000; // Convert Ticks to Milliseconds

                // Alternatively, in modern .NET:
                // _latestAssemblyTime = new DateTimeOffset(maxTime).ToUnixTimeMilliseconds();
            }

            context.Response.ContentType = "application/json";

            // Return the array: [ProcessStartTime, NewestFileTime]
            var versionData = new long[] { _appStartTime, _latestAssemblyTime.Value };
            var json = JsonSerializer.Serialize(versionData, JsonHelper.Default);

            await context.Response.WriteAsync(json);
        }


        public static async Task OnStatusCheck(HttpContext context)
        {
            string? tunnel = null;
            Exception? error = null;
            try
            {

                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                var cloudflaredSettings = JsonSerializer.Deserialize<HostingSettings>(jsonQuery);
                if (cloudflaredSettings?.ApiToken != null
                    && cloudflaredSettings?.TunnelName != null
                    && cloudflaredSettings?.AccountId != null)
                {
                    tunnel = await CheckTunnelStatus(cloudflaredSettings?.AccountId, cloudflaredSettings?.TunnelName, cloudflaredSettings?.ApiToken);
                }
                else
                {
                    tunnel = await CheckTunnelStatus();
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                error = ex;
            }

            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(new StatusResponse()
            {
                Host = Settings?.Domain,
                Tunnel = tunnel,
                Installed = CheckServiceInstalled(),
                Error = error?.Message
            }, JsonHelper.Default);

            await context.Response.WriteAsync(json);

        }

#endif

        public async Task<string?> GetToken()
        {
            return StatusResponse.ItWorks?[0];
        }

        public async Task<string?> GetHost()
        {
            return Settings?.Domain;
        }

        public async Task<string?> CheckTunnel(string? _account = null, string? _tunnel = null, string? _api = null)
        {
            // TODO: save from desktop app if tunnel is working
            return await CheckTunnelStatus(_account, _tunnel, _api);
        }

        private static DateTime? lastChecked;
        private static string? lastStatus;

        public event Action<bool?>? OnHttpWorking;

        public static async Task<string?> CheckTunnelStatus(string? _account = null, string? _tunnel = null, string? _api = null)
        {
            var AccountId = string.IsNullOrWhiteSpace(_account) ? Settings?.AccountId : _account;
            var TunnelName = string.IsNullOrWhiteSpace(_tunnel) ? Settings?.TunnelName : _tunnel;
            var ApiToken = string.IsNullOrWhiteSpace(_api) ? Settings?.ApiToken : _api;


            if (string.IsNullOrWhiteSpace(AccountId)
             || string.IsNullOrWhiteSpace(TunnelName)
             || string.IsNullOrWhiteSpace(ApiToken)
             || (lastStatus != null && lastChecked + TimeSpan.FromMinutes(2) > DateTime.Now))
            {
                return lastStatus;
            }

            try
            {
                _ = (_httpClient?.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken));

                var request = _httpClient?.GetAsync(
                    $"https://api.cloudflare.com/client/v4/accounts/{AccountId}/cfd_tunnel?name={TunnelName}&is_deleted=false");
                if (request == null) return null;
                var response = await request;
                if (response == null) return null;
                CloudflareResponse? result = await response.Content.ReadFromJsonAsync<CloudflareResponse>();
                if (result == null) return null;
                lastChecked = DateTime.Now;
                lastStatus = result.Result?.FirstOrDefault()?.Status ?? "Unknown";

                if (string.Equals(lastStatus, "healthy", StringComparison.InvariantCultureIgnoreCase))
                {
                    // TODO: notice don't edit domain in this save

                    // TODO: save connection info if it came from parameters in json file
                }

                return lastStatus;
            }
            catch (Exception ex)
            {
                throw new Exception("Status check error: " + ex.Message, ex);
            }
        }

        private StatusResponse? statusResult;
        private DateTime? statusChecked;

        public async Task<bool?> IsWorking()
        {
            var token = await GetToken();
            if (statusResult != null)
            {
                return token == StatusResponse.ItWorks?[0];
            }
            var host = await GetHost();
            var result = await CheckStatus(host);
            OnHttpWorking?.Invoke(token == StatusResponse.ItWorks?[0]);
            return token == StatusResponse.ItWorks?[0];
        }

        public async Task<StatusResponse?> CheckStatus(string? domain)
        {
            if (statusResult != null && statusChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return statusResult;
            }

            var cancellation = new CancellationToken();
            var request = _httpClient?.PostAsJsonAsync($"https://{domain}/api/status", new StringContent("", System.Text.Encoding.UTF8, "application/json"), cancellation);
            if (request == null) return null;
            var response = await request;
            var result = await response.Content.ReadFromJsonAsync<StatusResponse>(cancellationToken: cancellation);
            statusResult = result;
            statusChecked = DateTime.Now;
            return statusResult;
        }
    }

    public class CloudflareResponse { public List<TunnelInfo>? Result { get; set; } }
    public class TunnelInfo { public string? Status { get; set; } }

}
