using FlashCard.Services;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atrium.Services
{
    internal class ChatService : IChatService
    {
        static string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        internal static IServiceProvider? _services;
        //private readonly HttpClient? _httpClient;

        protected static List<ServicePreset>? Settings { get; set; }

        public ChatService()
        {
            //_httpClient = _services?.GetRequiredService<HttpClient>();
        }

        static ChatService()
        {
            var savedSettings = Path.Combine(homeDirectory, ".credentials", "study-sauce-chat.json");
            if (File.Exists(savedSettings))
            {
                Settings = JsonSerializer.Deserialize<List<ServicePreset>>(File.ReadAllText(savedSettings));
            }
        }

        const string PingMessage = "Please respond quickly and succinctly, you are learning tool with access to many functions. Please respond with the word Supercalifragilisticexpialidocious inside JSON format { \"response\" : \"...\" }. Only respond with the JSON and the word no other explanations needed. ";

        public async Task<bool?> PingService(string ServiceUrl, string ModelName, string ApiKey, List<DynamicParam> Parameters)
        {
            // TODO: save service information


            var json = await ExecutePost(ServiceUrl, ModelName, ApiKey, Parameters, PingMessage);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json ?? "");
            return parsed?.TryGetValue("response", out var response) == true
                && string.Equals(response, "Supercalifragilisticexpialidocious", StringComparison.InvariantCultureIgnoreCase);
        }


        public async Task<string?> SendMessage(string message)
        {
            return "";
        }


        public static async Task OnPresets(HttpContext context)
        {
            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(GetPresets(), new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
            });

            await context.Response.WriteAsync(json);
        }


        public static async Task OnPing(HttpContext context)
        {

            context.Response.ContentType = "application/json";

            try
            {

                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                var service = JsonSerializer.Deserialize<ServicePreset>(jsonQuery);

                // TODO: save preset settings in json file
                if (service == null)
                {
                    throw new InvalidOperationException("Request body failed to render.");
                }

                var _chat = _services?.GetRequiredService<ChatService>();

                if (_chat == null)
                {
                    throw new InvalidOperationException("Chat Service failed to render.");
                }

                var result = await _chat.PingService(service.Url, service.DefaultModel, service.ApiKey, service.Params);

                if (result == true)
                {
                    // TODO: store service information in JSON
                }

                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });

                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize("Error: " + ex.Message, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });

                await context.Response.WriteAsync(json);
            }

        }

        public static async Task OnChat(HttpContext context)
        {

            context.Response.ContentType = "application/json";

            try
            {

                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                var message = JsonSerializer.Deserialize<string>(jsonQuery);

                // TODO: save preset settings in json file
                if (message == null)
                {
                    throw new InvalidOperationException("Request message failed to render.");
                }

                var _chat = _services?.GetRequiredService<ChatService>();

                if (_chat == null)
                {
                    throw new InvalidOperationException("Chat Service failed to render.");
                }

                var result = await _chat.SendMessage(message);
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });

                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize("Error: " + ex.Message, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });

                await context.Response.WriteAsync(json);
            }
        }


        public static async Task<string?> ExecutePost(string ServiceUrl, string ModelName, string ApiKey, List<DynamicParam> Parameters, string FirstMessage)
        {
            if (_services == null) throw new InvalidOperationException("Assign services after app is created.");
            var Http = _services.GetRequiredService<HttpClient>();

            var payload = new Dictionary<string, object>();

            foreach (var p in Parameters)
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
                    var authScheme = ServiceUrl.Contains("anthropic") ? "x-api-key" : "Bearer";
                    if (authScheme == "Bearer")
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                    else
                        request.Headers.Add("x-api-key", ApiKey);
                }

                request.Content = JsonContent.Create(payload);
                var response = await Http.SendAsync(request);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }


        public async Task<List<ServicePreset>> ListPresets()
        {
            return GetPresets();
        }


        public static List<ServicePreset> GetPresets() => new() {
            new ServicePreset {
                Name = "Ollama (Local /generate)",
                Url = "http://localhost:11434/api/generate",
                DefaultModel = "llama3",
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "prompt", Value = "{Message}", Type = "string" },
                    new DynamicParam { Key = "stream", BoolValue = false, Type = "boolean" }
                }
            },
            new ServicePreset {
                Name = "OpenAI (v1/chat)",
                Url = "https://api.openai.com/v1/chat/completions",
                DefaultModel = "gpt-4o",
                ApiKey = "sk-...",
                Params = new() {
                    // Note: OpenAI expects a 'messages' object, 
                    // but for a generic builder, we track the key/value
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "temperature", Value = "0.8", Type = "number" },
                    new DynamicParam { Key = "max_tokens", Value = "500", Type = "number" },
                    new DynamicParam { Key = "top_p", Value = "1", Type = "number" },
                    new DynamicParam { Key = "presence_penalty", Value = "0", Type = "number" },
                    new DynamicParam { Key = "stream", BoolValue = false, Type = "boolean" }
                }
            },
            new ServicePreset {
                Name = "Groq (Cloud)",
                Url = "https://api.groq.com/openai/v1/chat/completions",
                DefaultModel = "llama-3.3-70b-versatile",
                ApiKey = "gsk_...",
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "temperature", Value = "0.5", Type = "number" }
                }
            },
            new ServicePreset {
                Name = "Anthropic (Claude)",
                Url = "https://api.anthropic.com/v1/messages",
                DefaultModel = "claude-3-5-sonnet-20240620",
                ApiKey = "sk-ant-api03-...",
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "max_tokens", Value = "1024", Type = "number" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
                }
            },
            new ServicePreset {
                Name = "DeepSeek",
                Url = "https://api.deepseek.com/chat/completions",
                DefaultModel = "deepseek-chat",
                ApiKey = "sk-...", // DeepSeek uses standard sk- format
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
                }
            },
            new ServicePreset {
                Name = "OpenRouter (Universal)",
                Url = "https://openrouter.ai/api/v1/chat/completions",
                DefaultModel = "google/gemini-2.0-flash-001",
                ApiKey = "sk-or-v1-...", // OpenRouter keys start with sk-or-v1-
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
                }
            },
            new ServicePreset {
                Name = "Mistral AI",
                Url = "https://api.mistral.ai/v1/chat/completions",
                DefaultModel = "mistral-tiny",
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "safe_prompt", BoolValue = true, Type = "boolean" }
                }
            },

            new ServicePreset {
                Name = "Local LLM (OpenAI Format)",
                Url = "http://localhost:1234/v1/chat/completions",
                DefaultModel = "local-model",
                Params = new() {
                    new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                    new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                    new DynamicParam { Key = "temperature", Value = "0.8", Type = "number" }
                }
            }
        };


    }


}
