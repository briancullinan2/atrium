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
        private Tuple<bool?, string?>? recentPing;
        private DateTime? recentPinged;
        public Dictionary<DateTime, Tuple<bool, string>>? Recents { get; set; } = new();
        public bool Chat { get; set; } = false;

        public event Action<bool?>? OnChatWorking;
        public event Action<bool>? OnChatChanged;
        public event Action? OnChatMessage;

        public async Task<bool?> IsWorking() => recentPing?.Item1 ?? (await PingService("", "", "", "", []))?.Item1;

        public ChatService()
        {
            _httpClient = _service?.GetRequiredService<HttpClient>();
            _ = PingService("", "", "", "", []);
        }

        static ChatService()
        {
            var _chat = _service?.GetRequiredService<ChatService>();
            _ = _chat?.IsWorking();
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

        public async Task<Tuple<bool?, string?>> PingService(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters)
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
                Params = Parameters,
                ResponsePath = Response
            }), System.Text.Encoding.UTF8, "application/json")).Result;
            if (response == null) return new Tuple<bool?, string?>(null, null);
            var result = await response.Content.ReadFromJsonAsync<Tuple<bool?, string?>>();
            recentPing = result;
            recentPinged = DateTime.Now;
            OnChatWorking?.Invoke(recentPing?.Item1);
            return recentPing ?? new Tuple<bool?, string?>(null, null);

        }

        public async Task<string?> SendMessage(string message)
        {
            if (recentMessage != null && recentMessaged + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                return recentMessage;
            }

            Recents?.Add(DateTime.Now, new Tuple<bool, string>(false, message));
            OnChatMessage?.Invoke();

            var response = _httpClient?.PostAsJsonAsync("/api/chat", new StringContent(JsonSerializer.Serialize(message), System.Text.Encoding.UTF8, "application/json")).Result;
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<string>();
            if (result == null) return null;
            recentMessage = result;
            recentMessaged = DateTime.Now;
            Recents?.Add(DateTime.Now + TimeSpan.FromMilliseconds(1), new Tuple<bool, string>(true, result));
            OnChatMessage?.Invoke();
            return recentMessage;
        }

        public async Task SetChatMode(bool chat)
        {
            Chat = chat;
            OnChatChanged?.Invoke(chat);
        }
    }
}
