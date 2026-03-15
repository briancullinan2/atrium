using Microsoft.AspNetCore.Http;
using FlashCard.Services;
using System.Net.Http.Json;
#if WINDOWS
using System.ServiceProcess;
#endif
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atrium.Services
{
    internal class StatusService : IStatusService
    {
        static string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        static HostingSettings? Settings { get; set; }
        internal static IServiceProvider? _services;

        static StatusService()
        {
            var savedSettings = Path.Combine(homeDirectory, ".credentials", "study-sauce-hosting.json");
            if (File.Exists(savedSettings))
            {
                Settings = JsonSerializer.Deserialize<HostingSettings>(File.ReadAllText(savedSettings));
            }
        }

        protected static HttpClient? _httpClient;
        public StatusService()
        {
            if (_httpClient == null)
            {
                _httpClient = _services?.GetRequiredService<HttpClient>();
            }
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
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
            });

            await context.Response.WriteAsync(json);

        }

        public async Task<string?> GetToken()
        {
            return (new StatusResponse()).ItWorks?[0];
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

        public static async Task<string?> CheckTunnelStatus(string? _account = null, string? _tunnel = null, string? _api = null)
        {
            var AccountId = _account ?? Settings?.AccountId;
            var TunnelName = _tunnel ?? Settings?.TunnelName;
            var ApiToken = _api ?? Settings?.ApiToken;


            if (string.IsNullOrWhiteSpace(AccountId)
             || string.IsNullOrWhiteSpace(TunnelName)
             || string.IsNullOrWhiteSpace(ApiToken)
             || (lastStatus != null && lastChecked + TimeSpan.FromMinutes(2) > DateTime.Now))
            {
                return lastStatus;
            }

            try
            {
                _httpClient?.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken);

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
    }

    public class CloudflareResponse { public List<TunnelInfo>? Result { get; set; } }
    public class TunnelInfo { public string? Status { get; set; } }

}
