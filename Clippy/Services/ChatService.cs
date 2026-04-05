using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using TaskExtensions = Extensions.PrometheusTypes.TaskExtensions;

namespace Clippy.Services
{
    public partial class ChatService(HttpClient Http) : IChatService
    {
        private List<ServicePreset>? recentResult;
        private DateTime? recentChecked;
        private string? recentMessage;
        private DateTime? recentMessaged;
        private Tuple<bool?, string?>? recentPing;
        private DateTime? recentPinged;

        public bool Chat { get; set; } = false;


        public bool? Working {
            get
            {
                _ = IsWorking();
                return recentPing?.Item1;
            }
        }


        public async Task SetChatMode(bool chat)
        {
            Chat = chat;
            OnChatChanged?.Invoke(chat);
        }


        private int recentHash;
        public static Dictionary<string, Dictionary<DateTime, Tuple<bool, string>>>? AllRecents { get; set; } = [];
        public Dictionary<DateTime, Tuple<bool, string>>? Recents
        {
            get
            {
                if (AllRecents?.TryGetValue("", out var _recents) == true) return _recents;
                var newRecent = new Dictionary<DateTime, Tuple<bool, string>>();
                _ = (AllRecents?[""] = newRecent);
                return newRecent;
            }
        }

        public async Task<Tuple<bool?, string?>> TryChat(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters)
        {
            var hash = HashCode.Combine(ServiceUrl, ModelName, ApiKey, Response, JsonSerializer.Serialize(Parameters));
            if (recentPing != null && recentHash == hash && recentPinged + TimeSpan.FromMinutes(2) > DateTime.Now)
            {
                return recentPing;
            }

            var json = await ExecutePost(Http, "", ServiceUrl, ModelName, ApiKey, Response, Parameters, PingMessage);
            bool? result = null;
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json ?? "");

                result = parsed?.TryGetValue("response", out var response) == true
                    && string.Equals(response?.ToString(), "Supercalifragilisticexpialidocious", StringComparison.InvariantCultureIgnoreCase);

                if(result == true)
                {
                    SaveWorkingSettings(ServiceUrl, ModelName, ApiKey, Response, Parameters);
                }
            }
            catch (Exception) { }


            recentPing = new Tuple<bool?, string?>(result, json);
            recentPinged = DateTime.Now;
            recentHash = hash;
            OnChatWorking?.Invoke(result);
            return recentPing;
        }

        public async Task<Tuple<bool?, string?>> PingRemote(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters)
        {
            var response = await Http.PostAsJsonAsync("/api/chat/ping", new StringContent(JsonSerializer.Serialize(new ServicePreset()
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



        [RequiresUnreferencedCode("serializing message data")]
        public class RecentModel
        {
            public DateTime Date { get; set; }
            public string? Role { get; set; }
            public string? Content { get; set; }
        }

        public async Task<string?> SendMessage(string message)
        {
            Recents?.Add(DateTime.Now, new Tuple<bool, string>(false, message));
            OnChatMessage?.Invoke();

            var result = await StandardResponse(Http, "", message);

            Recents?.Add(DateTime.Now + TimeSpan.FromMilliseconds(1), new Tuple<bool, string>(true, result ?? ""));
            OnChatMessage?.Invoke();
            return result;
        }

        private readonly SemaphoreSlim _gate = new(1, 1);

        public event Action<bool?>? OnChatWorking;
        public event Action<bool>? OnChatChanged;
        public event Action? OnChatMessage;

        private Task<Tuple<bool?, string?>>? _pingTask;

        public async Task<bool?> IsWorking()
        {
            return (recentPing = await (_pingTask = TaskExtensions.Debounce<Tuple<bool?, string?>>(ExecutePingAndClear))).Item1;
        }
        

        private async Task<Tuple<bool?, string?>> ExecutePingAndClear()
        {
            try
            {
                // 3. Actually run the expensive service interrogation
                var result = await PingService("", "", "", "", []);
                var working = result?.Item1;

                OnChatWorking?.Invoke(working);
                return result;
            }
            finally
            {
                // 4. Clear the task so the next call (after completion) 
                // can decide to trigger a fresh check if needed.
                _pingTask = null;
            }
        }


#if WINDOWS
       

        public static async Task OnPing(HttpContext context, IChatService _chat)
        {

            context.Response.ContentType = "application/json";

            try
            {

                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                // TODO: save preset settings in json file
                var service = JsonSerializer.Deserialize<ServicePreset>(jsonQuery) ?? throw new InvalidOperationException("Request body failed to render.");

                var result = await _chat.PingService(service.Url, service.DefaultModel, service.ApiKey, service.ResponsePath, service.Params);

                var json = JsonSerializer.Serialize(result, JsonExtensions.Default);

                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize("Error: " + ex.Message, JsonExtensions.Default);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(json);
            }

        }


        public static async Task OnChat(HttpContext context, HttpClient Http)
        {

            context.Response.ContentType = "application/json";

            try
            {

                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                // TODO: save preset settings in json file
                var message = JsonSerializer.Deserialize<string>(jsonQuery) ?? throw new InvalidOperationException("Request message failed to render.");

                string clientId = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";



                var result = await StandardResponse(Http, clientId, message);

                var json = JsonSerializer.Serialize(result, JsonExtensions.Default);

                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize("Error: " + ex.Message, JsonExtensions.Default);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(json);
            }
        }

#endif
        

        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _clientTokens = new();


        
        public async Task<List<ServicePreset>> ListPresets()
        {
            return GetPresets();
        }


        [AllowAnonymous]
        public static List<ServicePreset> GetPresets()
        {
            if (Settings == null) return [.. ServicePresets.Predefined];

            // 1. Create a lookup table (O(n) instead of repeated O(n) inside the loop)
            var predefinedMap = ServicePresets.Predefined.ToDictionary(p => p.Url, StringComparer.OrdinalIgnoreCase);

            // 2. Project settings with fallback to lookup
            var userPresets = Settings.Select(s => {
                predefinedMap.TryGetValue(s.Url, out var pre);
                return new ServicePreset
                {
                    Url = s.Url,
                    IsDefault = s.IsDefault,
                    Name = string.IsNullOrWhiteSpace(s.Name) ? pre?.Name ?? "" : s.Name,
                    ApiKey = pre?.ApiKey ?? "",
                    IsPrevious = s.IsPrevious,
                    DefaultModel = s.DefaultModel,
                    Params = s.Params,
                    ResponsePath = s.ResponsePath,
                };
            });

            // 3. Add predefined ones that aren't in settings
            var existingUrls = new HashSet<string>(Settings.Select(s => s.Url), StringComparer.OrdinalIgnoreCase);
            var remaining = ServicePresets.Predefined.Where(p => !existingUrls.Contains(p.Url));

            return [.. userPresets, .. remaining];
        }



        public async Task<string?> SendRemote(string message)
        {
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

            var response = await Http.PostAsJsonAsync("/api/chat", new StringContent(JsonSerializer.Serialize(message), System.Text.Encoding.UTF8, "application/json"));
            if (response == null) return null;
            var result = await response.Content.ReadFromJsonAsync<string?>();
            if (result == null) return null;
            recentMessage = result;
            Recents?.Add(DateTime.Now + TimeSpan.FromMilliseconds(1), new Tuple<bool, string>(true, result));
            OnChatMessage?.Invoke();
            return recentMessage;
        }

        

    }


}
