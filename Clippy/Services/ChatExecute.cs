using System;
using System.Collections.Generic;
using System.Text;

namespace Clippy.Services
{
    public partial class ChatService
    {
        public static async Task<string?> ExecutePost(HttpClient Http, string _client, string FirstMessage, ServicePreset? service = null)
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

            var ServiceUrl = string.IsNullOrWhiteSpace(service?.Url) ? bestService?.Url : service.Url;
            var ModelName = string.IsNullOrWhiteSpace(service?.DefaultModel) ? bestService?.DefaultModel : service.DefaultModel;
            var ApiKey = string.IsNullOrWhiteSpace(service?.ApiKey) ? bestService?.ApiKey : service.ApiKey;
            var Response = string.IsNullOrWhiteSpace(service?.ResponsePath) ? bestService?.ResponsePath : service.ResponsePath;
            var Parameters = service?.Parameters?.Count == 0 ? bestService?.Parameters : service?.Parameters;

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


    }
}
