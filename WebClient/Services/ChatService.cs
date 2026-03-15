using FlashCard.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebClient.Services
{
    public class ChatService : IChatService
    {
        private readonly HttpClient? _httpClient;
        internal static IServiceProvider? _service;
        private List<ServicePreset>? recentResult;
        private DateTime? recentChecked;
        private string? recentMessage;
        private DateTime? recentMessaged;
        private bool? recentPing;
        private DateTime? recentPinged;


        public ChatService()
        {
            _httpClient = _service?.GetRequiredService<HttpClient>();
            _ = PingService("", "", "", []);
        }

        public async Task<List<ServicePreset>> ListPresets()
        {
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult;
            }

            var response = _httpClient?.PostAsJsonAsync("/api/chat/presets", new StringContent("", System.Text.Encoding.UTF8, "application/json")).Result;
            if (response == null) return [];
            var result = await response.Content.ReadFromJsonAsync<List<ServicePreset>>();
            if (result == null) return [];
            recentResult = result;
            recentChecked = DateTime.Now;
            return recentResult;
        }

        public async Task<bool?> PingService(string ServiceUrl, string ModelName, string ApiKey, List<DynamicParam> Parameters)
        {
            if (recentPing != null && recentPinged + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                return recentPing;
            }


            var response = _httpClient?.PostAsJsonAsync("/api/chat/ping", new StringContent(JsonSerializer.Serialize(new ServicePreset()
            {
                ApiKey = ApiKey,
                DefaultModel = ModelName,
                Url = ServiceUrl,
                Params = Parameters
            }), System.Text.Encoding.UTF8, "application/json")).Result;
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<bool>();
            recentPing = result;
            recentPinged = DateTime.Now;
            return recentPing;

        }

        public async Task<string?> SendMessage(string message)
        {
            if (recentMessage != null && recentMessaged + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                return recentMessage;
            }


            var response = _httpClient?.PostAsJsonAsync("/api/chat", new StringContent(JsonSerializer.Serialize(message), System.Text.Encoding.UTF8, "application/json")).Result;
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<string>();
            if (result == null) return null;
            recentMessage = result;
            recentMessaged = DateTime.Now;
            return recentMessage;
        }
    }
}
