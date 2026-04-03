using System.Net.Http.Json;
using System.Text.Json;

namespace Clippy.Services
{
    public class ChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private List<ServicePreset>? recentResult;
        private DateTime? recentChecked;
        private string? recentMessage;
        private DateTime? recentMessaged;
        private Tuple<bool?, string?>? recentPing;
        private DateTime? recentPinged;
        public Dictionary<DateTime, Tuple<bool, string>>? Recents { get; set; } = [];
        public bool Chat { get; set; } = false;

        public event Action<bool?>? OnChatWorking;
        public event Action<bool>? OnChatChanged;
        public event Action? OnChatMessage;

        private Task<bool?>? _pingTask;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<bool?> IsWorking()
        {
            if (recentPing != null && recentPinged != null && recentPinged + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentPing.Item1;
            }

            // 1. If a task is already in progress, everyone just awaits the same one
            if (_pingTask != null)
            {
                return await _pingTask;
            }

            await _gate.WaitAsync();
            try
            {
                // 2. Double-check inside the lock to prevent race conditions
                // Start the task but don't await it here yet
                _pingTask ??= ExecutePingAndClear();
            }
            finally
            {
                _ = _gate.Release();
            }

            return await _pingTask;
        }

        private async Task<bool?> ExecutePingAndClear()
        {
            try
            {
                // 3. Actually run the expensive service interrogation
                var result = await PingService("", "", "", "", []);
                var working = result?.Item1;

                OnChatWorking?.Invoke(working);
                return working;
            }
            finally
            {
                // 4. Clear the task so the next call (after completion) 
                // can decide to trigger a fresh check if needed.
                _pingTask = null;
            }
        }

        public static Task<bool?>? Working { get; set; }

        public ChatService(HttpClient client)
        {
            _httpClient = client;
            Working ??= IsWorking();
        }


        public async Task<List<ServicePreset>> ListPresets()
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("Http client unavailable.");
            }
            if (recentResult != null && recentChecked + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentResult;
            }

            var response = await _httpClient.PostAsJsonAsync("/api/chat/presets", new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            if (response == null) return [];
            var result = await response.Content.ReadFromJsonAsync<List<ServicePreset>>();
            if (result == null) return [];
            recentResult = result;
            recentChecked = DateTime.Now;
            return recentResult;
        }

        public async Task<Tuple<bool?, string?>> PingService(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("Http client unavailable.");
            }


            if (recentPing != null && recentPinged + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentPing;
            }


            var response = await _httpClient.PostAsJsonAsync("/api/chat/ping", new StringContent(JsonSerializer.Serialize(new ServicePreset()
            {
                ApiKey = ApiKey,
                DefaultModel = ModelName,
                Url = ServiceUrl,
                Params = Parameters,
                ResponsePath = Response
            }), System.Text.Encoding.UTF8, "application/json"));

            var result = await response.Content.ReadFromJsonAsync<Tuple<bool?, string?>>();
            recentPing = result;
            recentPinged = DateTime.Now;
            Console.WriteLine("Chat working: " + result);
            OnChatWorking?.Invoke(recentPing?.Item1);
            return recentPing ?? new Tuple<bool?, string?>(null, null);

        }

        public async Task<string?> SendMessage(string message)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("Http client unavailable.");
            }

            Recents?.Add(DateTime.Now, new Tuple<bool, string>(false, message));
            OnChatMessage?.Invoke();

            if (recentMessage != null && recentMessaged + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                recentMessaged = DateTime.Now; // prevent them from flooding
                Recents?.Add(DateTime.Now + TimeSpan.FromMilliseconds(1), new Tuple<bool, string>(true, "You're sending messages too quickly."));
                OnChatMessage?.Invoke();
                return null;
            }
            recentMessaged = DateTime.Now;

            var response = await _httpClient.PostAsJsonAsync("/api/chat", new StringContent(JsonSerializer.Serialize(message), System.Text.Encoding.UTF8, "application/json"));
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<string?>();
            if (result == null) return null;
            recentMessage = result;
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
