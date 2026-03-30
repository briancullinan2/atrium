using DataLayer.Utilities;
using FlashCard.Services;
#if WINDOWS
using Microsoft.AspNetCore.Http;
#endif
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace Atrium.Services
{
    internal class ChatService : IChatService
    {
        private static readonly string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static readonly string savedSettings = Path.Combine(homeDirectory, ".credentials", "atrium-chat.json");

        protected static List<ServicePreset>? Settings { get; set; } = [];
        public readonly HttpClient Http;
        public static Task<bool?>? Working { get; set; }

        public ChatService(HttpClient client)
        {
            Http = client;
            Working ??= IsWorking();
        }


        static ChatService()
        {
            if (System.IO.File.Exists(savedSettings))
            {
                try
                {
                    Settings = JsonSerializer.Deserialize<List<ServicePreset>>(System.IO.File.ReadAllText(savedSettings));
                }
                catch (Exception) { }
            }
        }

        private const string PingMessage = "Please respond quickly and succinctly, you are learning tool with access to many functions. Please respond with the word Supercalifragilisticexpialidocious inside JSON format { \"response\" : \"...\" }. Only respond with the JSON and the word no other explanations needed. ";
        private int recentHash;
        private Tuple<bool?, string?>? recentPing;
        private DateTime? recentPinged;
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

        public bool Chat { get; set; } = false;

        public async Task<Tuple<bool?, string?>> PingService(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters)
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

            }
            catch (Exception) { }


            if (result == true && Settings != null && !string.IsNullOrWhiteSpace(ServiceUrl))
            {
                // TODO: save service information
                var index = Settings.FindIndex(s => string.Equals(s.Url, ServiceUrl, StringComparison.InvariantCultureIgnoreCase));
                var replacementPreset = new ServicePreset()
                {
                    ApiKey = ApiKey,
                    Url = ServiceUrl,
                    DefaultModel = ModelName,
                    ResponsePath = Response,
                    Params = Parameters,
                    IsPrevious = true,
                };

                if (Settings.Count == 0 || Settings.FirstOrDefault(s => s.IsDefault) == null)
                {
                    replacementPreset.IsDefault = true;
                }

                if (index > -1)
                {
                    Settings[index] = replacementPreset;
                }
                else
                {
                    Settings.Add(replacementPreset);
                }

                var validSettings = JsonSerializer.Serialize(Settings, JsonHelper.Default);
                System.IO.File.WriteAllText(savedSettings, validSettings);
            }
            recentPing = new Tuple<bool?, string?>(result, json);
            recentPinged = DateTime.Now;
            recentHash = hash;
            OnChatWorking?.Invoke(result);
            return recentPing;
        }


        public class RecentModel
        {
            public DateTime Date { get; set; }
            public string? Role { get; set; }
            public string? Content { get; set; }
        }

        public async Task<string?> SendMessage(string message)
        {
            var previous = JsonSerializer.Serialize(Recents?.TakeLast(10).Select(r => new RecentModel()
            {
                Role = r.Value.Item1 ? "assistant" : "user",
                Date = r.Key,
                Content = r.Value.Item2
            }));
            Recents?.Add(DateTime.Now, new Tuple<bool, string>(false, message));
            OnChatMessage?.Invoke();

            var result = await StandardResponse(Http, "", message);

            Recents?.Add(DateTime.Now + TimeSpan.FromMilliseconds(1), new Tuple<bool, string>(true, result ?? ""));
            OnChatMessage?.Invoke();
            return result;
        }

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


#if WINDOWS
        public static async Task OnPresets(HttpContext context)
        {
            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(GetPresets(), JsonHelper.Default);

            await context.Response.WriteAsync(json);
        }


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

                var json = JsonSerializer.Serialize(result, JsonHelper.Default);

                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize("Error: " + ex.Message, JsonHelper.Default);
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

                var json = JsonSerializer.Serialize(result, JsonHelper.Default);

                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize("Error: " + ex.Message, JsonHelper.Default);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(json);
            }
        }

#endif
        public static async Task<string?> StandardResponse(HttpClient Http, string clientId, string message)
        {

            Dictionary<DateTime, Tuple<bool, string>>? _recents = null;
            if (AllRecents?.TryGetValue(clientId, out _recents) != true)
            {
                _ = (AllRecents?[clientId] = _recents = []);
            }


            if (_recents?.LastOrDefault().Key != null && _recents?.Last().Key + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                _recents?.Add(DateTime.Now + TimeSpan.FromSeconds(1), new Tuple<bool, string>(true, "You're sending messages too quickly."));
                return null;
            }


            var previous = JsonSerializer.Serialize(_recents?.TakeLast(10).Select(r => new RecentModel()
            {
                Role = r.Value.Item1 ? "assistant" : "user",
                Date = r.Key,
                Content = r.Value.Item2
            }));

            var result = await ExecutePost(Http, clientId, "", "", "", "", [], "The user writes:\n" + message
                + "\n\nIf it's directly related to a command, respond with JSON only like "
                + "{\"Function\": \"...\", \"Param1\" : \"...\"}. If you need to chain informational "
                + "commands together, use a list []:\n" + CommandString + "\n\nHistory for Context:\n"
                + previous);

            return result;
        }


        public static string CommandString => string.Join("\n", ChatCommand.CommandRegistry.Select(c => c.Function + c.Parameters + " - " + c.Description));

        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _clientTokens = new();


        public static async Task<string?> ExecutePost(HttpClient Http, string _client, string _service, string _model, string _key, string _response, List<DynamicParam> _parameters, string FirstMessage)
        {

            // --- Cancellation Logic ---
            // Cancel and dispose of any existing request for this client
            if (_clientTokens.TryRemove(_client, out var oldCts))
            {
                oldCts.Cancel();
            }

            // Create a new token for this specific request
            var cts = new CancellationTokenSource();
            _clientTokens[_client] = cts;


            var bestService = Settings?.FirstOrDefault(s => s.IsPrevious)
                ?? Settings?.FirstOrDefault(s => s.IsDefault)
                ?? Settings?.FirstOrDefault();

            var ServiceUrl = string.IsNullOrWhiteSpace(_service) ? bestService?.Url : _service;
            var ModelName = string.IsNullOrWhiteSpace(_model) ? bestService?.DefaultModel : _model;
            var ApiKey = string.IsNullOrWhiteSpace(_key) ? bestService?.ApiKey : _key;
            var Response = string.IsNullOrWhiteSpace(_response) ? bestService?.ResponsePath : _response;
            var Parameters = _parameters?.Count == 0 ? bestService?.Params : _parameters;

            var payload = new Dictionary<string, object>();

            foreach (var p in Parameters ?? [])
            {
                // 1. Handle Placeholder Replacements for Strings
                var processedValue = p.Value;
                if (p.Type == "string" && processedValue != null)
                {
                    processedValue = processedValue
                        .Replace("{Message}", FirstMessage)
                        .Replace("{Model}", ModelName);
                }

                // 2. Parse Types and add to Payload
                if (p.Type == "number" && double.TryParse(processedValue, out var n))
                    payload[p.Key] = n;
                else if (p.Type == "boolean")
                    payload[p.Key] = p.BoolValue;
                else
                    payload[p.Key] = processedValue ?? "";
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ServiceUrl);

                // Add Authorization header if key exists
                if (!string.IsNullOrEmpty(ApiKey) && ApiKey != "not-needed")
                {
                    // Anthropic uses a different header name, but standard is Bearer
                    var authScheme = ServiceUrl?.Contains("anthropic") == true ? "x-api-key" : "Bearer";
                    if (authScheme == "Bearer")
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                    else
                        request.Headers.Add("x-api-key", ApiKey);
                }

                request.Content = JsonContent.Create(payload);
                var task = Http?.SendAsync(request, cts.Token) ?? throw new InvalidOperationException("Could not create request.");
                var response = await task;
                var result = await response.Content.ReadAsStringAsync();
                return ExtractValue(result, Response);
            }
            catch (OperationCanceledException)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return "Request cancelled by new input.";
                }
                return "Request timed out.";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
            finally
            {
                // Cleanup: remove the token if this specific task is the one that finished
                if (_clientTokens.TryGetValue(_client, out var currentCts) && currentCts == cts)
                {
                    _ = _clientTokens.TryRemove(_client, out _);
                    cts.Dispose();
                }
            }
        }

        public static string ExtractValue(string json, string? path)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var element = doc.RootElement;

                foreach (var segment in path?.Split('.') ?? [])
                {
                    if (segment.Contains('[') && segment.Contains(']'))
                    {
                        // Handle Array Access: "choices[0]"
                        var parts = segment.Split('[');
                        var name = parts[0];
                        var index = int.Parse(parts[1].Replace("]", ""));
                        element = element.GetProperty(name)[index];
                    }
                    else
                    {
                        // Handle Property Access: "message"
                        element = element.GetProperty(segment);
                    }
                }
                return element.GetString() ?? element.GetRawText();
            }
            catch (Exception ex)
            {
                return $"[Extraction Error: {ex.Message}]";
            }
        }

        public async Task<List<ServicePreset>> ListPresets()
        {
            return GetPresets();
        }

        public static List<ServicePreset> GetPresets()
        {
            return Settings?.Select(s => new ServicePreset()
            {
                Url = s.Url,
                IsDefault = s.IsDefault,
                IsPrevious = s.IsPrevious,
                ApiKey = PredefinedServices.FirstOrDefault(p => string.Equals(s.Url, p.Url))?.ApiKey ?? "",
                DefaultModel = s.DefaultModel,
                Params = s.Params,
                ResponsePath = s.ResponsePath,
                Name = string.IsNullOrWhiteSpace(s.Name) ? PredefinedServices.FirstOrDefault(p => string.Equals(s.Url, p.Url))?.Name ?? "" : s.Name
            }).Concat(PredefinedServices.Where(p => Settings?.FirstOrDefault(s => string.Equals(s.Url, p.Url, StringComparison.InvariantCultureIgnoreCase)) == null)).ToList() ?? [];
        }

        public async Task SetChatMode(bool chat)
        {
            Chat = chat;
            OnChatChanged?.Invoke(chat);
        }

        internal static List<ServicePreset> PredefinedServices = [
            new ServicePreset {
                Name = "Ollama (Local /generate)",
                Url = "http://localhost:11434/api/generate",
                DefaultModel = "qwen3.5:cloud",
                ResponsePath = "response",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "prompt", Value = "{Message}", Type = "string" },
                    new DynamicParam { Key = "stream", BoolValue = false, Type = "boolean" }
                ]
            },
            new ServicePreset {
                Name = "OpenAI (v1/chat)",
                Url = "https://api.openai.com/v1/chat/completions",
                DefaultModel = "gpt-4o",
                ApiKey = "sk-...",
                ResponsePath = "choices[0].message.content",
                Params = [
                    // Note: OpenAI expects a 'messages' object, 
                    // but for a generic builder, we track the key/value
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "temperature", Value = "0.8", Type = "number" },
                    new DynamicParam { Key = "max_tokens", Value = "500", Type = "number" },
                    new DynamicParam { Key = "top_p", Value = "1", Type = "number" },
                    new DynamicParam { Key = "presence_penalty", Value = "0", Type = "number" },
                    new DynamicParam { Key = "stream", BoolValue = false, Type = "boolean" }
                ]
            },
            new ServicePreset {
                Name = "Groq (Cloud)",
                Url = "https://api.groq.com/openai/v1/chat/completions",
                DefaultModel = "llama-3.3-70b-versatile",
                ApiKey = "gsk_...",
                ResponsePath = "choices[0].message.content",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "temperature", Value = "0.5", Type = "number" }
                ]
            },
            new ServicePreset {
                Name = "Anthropic (Claude)",
                Url = "https://api.anthropic.com/v1/messages",
                DefaultModel = "claude-3-5-sonnet-20240620",
                ApiKey = "sk-ant-api03-...",
                ResponsePath = "content[0].text",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "max_tokens", Value = "1024", Type = "number" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
                ]
            },
            new ServicePreset {
                Name = "DeepSeek",
                Url = "https://api.deepseek.com/chat/completions",
                DefaultModel = "deepseek-chat",
                ApiKey = "sk-...", // DeepSeek uses standard sk- format
                ResponsePath = "choices[0].message.content",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
                ]
            },
            new ServicePreset {
                Name = "OpenRouter (Universal)",
                Url = "https://openrouter.ai/api/v1/chat/completions",
                DefaultModel = "google/gemini-2.0-flash-001",
                ApiKey = "sk-or-v1-...", // OpenRouter keys start with sk-or-v1-
                ResponsePath = "choices[0].message.content",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
                ]
            },
            new ServicePreset {
                Name = "Mistral AI",
                Url = "https://api.mistral.ai/v1/chat/completions",
                DefaultModel = "mistral-tiny",
                ResponsePath = "choices[0].message.content",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "safe_prompt", BoolValue = true, Type = "boolean" }
                ]
            },

            new ServicePreset {
                Name = "LLM Studio (OpenAI Format)",
                Url = "http://localhost:1234/v1/chat/completions",
                DefaultModel = "local-model",
                ResponsePath = "choices[0].message.content",
                Params = [
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "temperature", Value = "0.8", Type = "number" }
                ]
            }
        ];

        public event Action<bool?>? OnChatWorking;
        public event Action<bool>? OnChatChanged;
        public event Action? OnChatMessage;
    }


}
