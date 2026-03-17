using Microsoft.AspNetCore.Http;
using FlashCard.Services;
using System.Net.Http.Json;
using DataLayer.Utilities;

#if WINDOWS
using System.ServiceProcess;
#endif
using System.Text.Json;

namespace Atrium.Services
{
    internal class StatusService : IStatusService
    {
        private static readonly string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static HostingSettings? Settings { get; set; }
        internal static IServiceProvider? _services;

        static StatusService()
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

            var _status = _services?.GetRequiredService<StatusService>();
            _ = _status?.IsWorking();
        }

        protected static HttpClient? _httpClient;
        public StatusService()
        {
            _httpClient ??= _services?.GetRequiredService<HttpClient>();
        }

        public async Task<bool?> CheckInstalled()
        {
            return StatusService.CheckServiceInstalled();
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
        public static async Task OnStatusCheck(HttpContext context)
        {
            string? tunnel = null;
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
            catch (Exception)
            {

            }

            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(new StatusResponse()
            {
                Host = Settings?.Domain,
                Tunnel = tunnel,
                Installed = CheckServiceInstalled()
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
                return "Error: " + ex.Message;
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
