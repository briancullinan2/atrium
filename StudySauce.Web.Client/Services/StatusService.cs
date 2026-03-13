using StudySauce.Shared.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudySauce.Web.Client.Services
{
    public class StatusService : IStatusService
    {
        private readonly HttpClient? _httpClient;
        internal static IServiceProvider? _service;
        private StatusResponse? recentResult;
        private DateTime? recentChecked;

        public StatusService()
        {
            _httpClient = _service?.GetRequiredService<HttpClient>();
        }

        public async Task<string?> GetToken()
        {
            return await GetToken(null, null, null);
        }

        public async Task<string?> GetToken(string? _account = null, string? _tunnel = null, string? _api = null)
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.ItWorks?[0];
            }
            var response = _httpClient?.PostAsJsonAsync("/api/status", new StringContent(JsonSerializer.Serialize(new Dictionary<string, string?>() {
                { "AccountId", _account },
                { "TunnelName", _tunnel },
                { "ApiToken", _api },
            }), System.Text.Encoding.UTF8, "application/json")).Result;
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
            if (result == null) return null;
            recentResult = result;
            recentChecked = DateTime.Now;
            return result.ItWorks?[0];
        }

        public async Task<string?> GetHost()
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.Host;
            }
            await GetToken();
            return recentResult?.Host;
        }

        public async Task<string?> CheckTunnel(string? _account = null, string? _tunnel = null, string? _api = null)
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.Tunnel;
            }
            await GetToken(_account, _tunnel, _api);
            return recentResult?.Tunnel;
        }

        public async Task<bool?> CheckInstalled()
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.Installed;
            }
            await GetToken();
            return recentResult?.Installed;
        }
    }
}
