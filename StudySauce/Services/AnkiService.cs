using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudySauce.Shared.Services;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudySauce.Services
{
    public class AnkiService : IAnkiService
    {
        internal static IServiceProvider? _services;
        private readonly HttpClient? _httpClient;

        public AnkiService()
        {
            _httpClient = _services?.GetRequiredService<HttpClient>();
        }

        public async Task<Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<DataLayer.Entities.Card>>> InspectFile(string ankiPackage)
        {
            try
            {
                var files = AnkiParser.Parser.ListFiles(ankiPackage, _services);
                var cards = AnkiParser.Parser.ParseCards(ankiPackage, _services);
                return new Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<DataLayer.Entities.Card>>(files, cards);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<Card>>([], []);
            }
        }

        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _userRequests = new();
        private static readonly SemaphoreSlim _ankiLock = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;

        private const int DelaySeconds = 30;

        protected static async Task<IEnumerable<DataLayer.Entities.File>> SearchAnki(string clientId, string? searchTerm, HttpClient _httpClient)
        {

            // 1. Cancel previous pending request for THIS client
            if (_userRequests.TryRemove(clientId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _userRequests[clientId] = cts;

            try
            {
                // 2. Enforce the 3-second window
                await _ankiLock.WaitAsync(cts.Token);

                var timeSinceLast = DateTime.UtcNow - _lastRequestTime;
                if (timeSinceLast.TotalSeconds < DelaySeconds)
                {
                    await Task.Delay(TimeSpan.FromSeconds(DelaySeconds) - timeSinceLast, cts.Token);
                }

                // 3. Execute the actual search
                var results = await DoActualSearch(searchTerm, _httpClient);

                _lastRequestTime = DateTime.UtcNow;
                return results;
            }
            catch (OperationCanceledException)
            {
                // This happens when a newer keystroke comes in and cancels this task
                return Enumerable.Empty<DataLayer.Entities.File>();
            }
            finally
            {
                _ankiLock.Release();
            }
        }

        protected static async Task<IEnumerable<DataLayer.Entities.File>> DoActualSearch(string? searchTerm, HttpClient _httpClient)
        {
            // We have to mimic a real browser for SvelteKit apps to respond or find their internal JSON route
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://ankiweb.net");

            // NOTE: If AnkiWeb detects a non-browser, they serve the shell you saw.
            // If we hit the search route with the right headers, we often get the pre-rendered 
            // data or the direct JSON. 

            // Some SvelteKit apps use a __data.json suffix for direct data access
            var dataUrl = $"https://ankiweb.net/svc/shared/list-decks?search={Uri.EscapeDataString(searchTerm ?? "")}";

            // Get the raw bytes since it's binary Protobuf data
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, dataUrl);

                // Headers copied from your successful browser trace
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Referer", $"https://ankiweb.net/shared/decks?search={Uri.EscapeDataString(searchTerm ?? "")}&sort=rating");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
                request.Headers.Add("sec-fetch-site", "same-origin");
                request.Headers.Add("sec-fetch-mode", "cors");
                request.Headers.Add("sec-fetch-dest", "empty");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var files = new List<DataLayer.Entities.File>();

                // Converting to string using UTF8 to find markers
                // Protobuf strings are encoded in UTF8
                string content = System.Text.Encoding.UTF8.GetString(bytes);

                // We'll use a sliding window to find the "Title" strings.
                // In your dump, titles follow a pattern of non-ASCII bytes followed by the name.
                string[] entries = content.Split(new[] { '\u0012' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in entries)
                {
                    if (entry.Length < 5) continue;

                    // The first byte of the split is usually the length. 
                    // We'll take the text until we hit a non-printable character or a known Protobuf tag.
                    string title = ExtractCleanString(entry);

                    if (IsLikelyAnkiTitle(title))
                    {
                        // The ID is usually embedded in the binary nearby. 
                        // In Anki's current API, the ID is a 10-digit number.
                        string id = ExtractAnkiId(entry);

                        if (!string.IsNullOrEmpty(id))
                        {
                            files.Add(new DataLayer.Entities.File
                            {
                                Source = title,
                                Url = $"https://ankiweb.net/shared/info/{id}"
                            });
                        }
                    }
                }

                return files.DistinctBy(f => f.Source).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return [];
        }

        private static string ExtractCleanString(string segment)
        {
            // Grab characters until we hit binary control data
            var clean = new string(segment.TakeWhile(c => c >= 32 && c <= 126 || c > 160).ToArray());
            return clean.Trim();
        }

        private static string ExtractAnkiId(string segment)
        {
            // Look for a sequence of 8-12 digits in the binary segment
            var match = System.Text.RegularExpressions.Regex.Match(segment, @"\d{8,12}");
            return match.Success ? match.Value : "";
        }

        private static bool IsLikelyAnkiTitle(string title)
        {
            // Filter out binary noise that looks like strings
            return title.Length > 5 && !title.Contains("http") && !title.Contains("{");
        }


        public static async Task OnDownloadAnki(HttpContext context, IServiceProvider _service)
        {
            var ankiService = _services.GetRequiredService<AnkiService>();
            var ankiPackage = context.Request.Query["anki"].ToString() ?? "";
            var results = await ankiService.Download(ankiPackage);
            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
            });
            await context.Response.WriteAsync(json);
        }


        public static async Task OnSearchAnki(HttpContext context, IServiceProvider _service)
        {
            try
            {
                var searchTerm = context.Request.Query["term"].ToString() ?? "";
                var _httpClient = _services?.GetRequiredService<HttpClient>();
                if (_httpClient == null)
                {
                    throw new InvalidOperationException("HttpClient unavailable");
                }
                // Unique ID for the client (IP or Session)
                string clientId = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                var results = await SearchAnki(clientId, searchTerm, _httpClient);
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(ex.Message, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });
                await context.Response.WriteAsync(json);
            }
        }


        public static async Task OnInspectFile(HttpContext context, IServiceProvider _service)
        {
            try
            {
                var files = AnkiParser.Parser.ListFiles(context.Request.Query["anki"], _services);
                var cards = AnkiParser.Parser.ParseCards(context.Request.Query["anki"], _services);
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new Inspection()
                {
                    Files = files,
                    Cards = cards
                }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });
                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(ex.Message, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });
                await context.Response.WriteAsync(json);
            }
        }

        public async Task<IEnumerable<DataLayer.Entities.File>?> Search(string? term)
        {
            if (_httpClient == null)
            {
                return [];
            }
            return await SearchAnki("127.0.0.1", term, _httpClient);
        }

        public async Task<IEnumerable<DataLayer.Entities.File>> Download(string ankiPackageUrl)
        {

            // ResponseHeadersRead ensures we don't buffer the whole file into RAM first
            using var response = await _httpClient?.GetAsync(ankiPackageUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var remoteStream = await response.Content.ReadAsStreamAsync();

            // We pass the stream directly to our generalized upload function
            // Using the URL's filename as the local path hint
            var fileName = Path.GetFileName(new Uri(ankiPackageUrl).LocalPath);
            var manager = _services?.GetRequiredService<FileManager>();
            await manager.UploadFile(remoteStream, fileName, "AnkiDownloads");

            // Return the entity (you'll likely want to fetch the record created in UploadFile)
            var PersistentFactory = _services.GetRequiredService<IDbContextFactory<DataLayer.PersistentStorage>>();
            var MemoryFactory = _services.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
            using var fileContext = PersistentFactory.CreateDbContext();
            using var memoryContext = MemoryFactory.CreateDbContext();
            await fileContext.Sync(memoryContext, (DataLayer.Entities.File f) => f.Source == "Upload" || f.Source == "AnkiDownloads");
            return await memoryContext.Files.Where(f => f.Source == "Upload" || f.Source == "AnkiDownloads").ToListAsync();
        }


        public class Inspection
        {
            public List<DataLayer.Entities.File> Files { get; set; }
            public List<DataLayer.Entities.Card> Cards { get; set; }

        }

    }
}
