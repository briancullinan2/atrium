using FlashCard.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebClient.Services
{
    public class HostingService : IHostingService
    {
        private static HttpClient? _httpClient;
        private StatusResponse? recentResult;
        private DateTime? recentChecked;
        private StatusResponse? statusResult;
        private DateTime? statusChecked;

        public event Action<bool?>? OnHttpWorking;
        public static Task<bool?>? Working { get; set; }

        public HostingService(HttpClient client)
        {
            _httpClient ??= client;
            Working ??= IsWorking();
        }

        public async Task<string?> GetToken()
        {
            return await GetToken(null, null, null);
        }


        public async Task<string?> GetToken(string? Account = null, string? Tunnel = null, string? Api = null)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("Http client unavailable.");
            }

            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return StatusResponse.ItWorks?[0];
            }
            var response = await _httpClient.PostAsJsonAsync("/api/status", new StringContent(JsonSerializer.Serialize(
            new HostingSettings()
            {
                AccountId = Account,
                TunnelName = Tunnel,
                ApiToken = Api
            }), System.Text.Encoding.UTF8, "application/json"));
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
            if (result == null) return null;
            recentResult = result;
            recentChecked = DateTime.Now;
            return StatusResponse.ItWorks?[0];
        }

        public async Task<string?> GetHost()
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.Host;
            }
            _ = await GetToken();
            return recentResult?.Host;
        }

        public async Task<string?> CheckTunnel(string? _account = null, string? _tunnel = null, string? _api = null)
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.Tunnel;
            }
            _ = await GetToken(_account, _tunnel, _api);
            return recentResult?.Tunnel;
        }

        public async Task<bool?> CheckInstalled()
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult.Installed;
            }
            _ = await GetToken();
            return recentResult?.Installed;
        }

        public async Task<bool?> IsWorking()
        {
            if (recentResult != null && statusResult != null)
            {
                return StatusResponse.ItWorks?[0] == StatusResponse.ItWorks?[0];
            }
            var token = await GetToken();
            var host = await GetHost();
            var result = await CheckStatus(host);
            OnHttpWorking?.Invoke(StatusResponse.ItWorks?[0] == token);
            return StatusResponse.ItWorks?[0] == token;
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
}
