using AnkiParser;
using DataLayer.Entities;
using DataLayer.Utilities;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Atrium.Services
{
    public partial class AnkiService(HttpClient _httpClient, IFileManager FileManager, IQueryManager Query) : IAnkiService
    {

        public async Task<Tuple<IEnumerable<DataLayer.Entities.File>?, IEnumerable<Card>?>> InspectFile(string ankiPackage)
        {

            try
            {
                var files = await AnkiParser.Parser.ListFiles(ankiPackage, Query);
                var cards = await AnkiParser.Parser.ParseCards(ankiPackage, Query);
                return new Tuple<IEnumerable<DataLayer.Entities.File>?, IEnumerable<Card>?>(files, cards);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new Tuple<IEnumerable<DataLayer.Entities.File>?, IEnumerable<Card>?>([], []);
            }
        }


        protected static async Task<IEnumerable<DataLayer.Entities.File>?> SearchAnki(string clientId, string? searchTerm, HttpClient HttpClient, [CallerFilePath] string path = "")
        {
            return await DataLayer.Utilities.Extensions.TaskExtensions.Debounce(DoActualSearch, 3000, searchTerm, HttpClient, new StackFrame(true).GetFileName() ?? path, nameof(SearchAnki) + ":" + clientId);
        }

        protected static async Task<IEnumerable<DataLayer.Entities.File>?> DoActualSearch(string? searchTerm, HttpClient? HttpClient)
        {
            if (HttpClient == null)
            {
                throw new InvalidOperationException("Http client not available.");
            }
            // We have to mimic a real browser for SvelteKit apps to respond or find their internal JSON route
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            HttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            HttpClient.DefaultRequestHeaders.Add("Origin", "https://ankiweb.net");

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

                var response = await HttpClient.SendAsync(request);
                _ = response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var files = new List<DataLayer.Entities.File>();

                // Converting to string using UTF8 to find markers
                // Protobuf strings are encoded in UTF8
                string content = System.Text.Encoding.UTF8.GetString(bytes);

                // We'll use a sliding window to find the "Title" strings.
                // In your dump, titles follow a pattern of non-ASCII bytes followed by the name.
                string[] entries = content.Split(['\u0012'], StringSplitOptions.RemoveEmptyEntries);

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

                return [.. files.DistinctBy(f => f.Source)];
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
            var clean = new string([.. segment.TakeWhile(c => c >= 32 && c <= 126 || c > 160)]);
            return clean.Trim();
        }

        private static string ExtractAnkiId(string segment)
        {
            // Look for a sequence of 8-12 digits in the binary segment
            var match = AnkiIdRegex().Match(segment);
            return match.Success ? match.Value : "";
        }

        private static bool IsLikelyAnkiTitle(string title)
        {
            // Filter out binary noise that looks like strings
            return title.Length > 5 && !title.Contains("http") && !title.Contains('{');
        }

#if WINDOWS
        public static async Task OnDownloadAnki(HttpContext context, AnkiService AnkiService)
        {
            var ankiPackage = context.Request.Query["anki"].ToString() ?? "";
            var results = await AnkiService.Download(ankiPackage);
            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(results, JsonHelper.Default);
            await context.Response.WriteAsync(json);
        }


        public static async Task OnSearchAnki(HttpContext context, HttpClient _httpClient)
        {
            try
            {
                var searchTerm = context.Request.Query["term"].ToString() ?? "";
                // Unique ID for the client (IP or Session)
                string clientId = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                var results = await SearchAnki(clientId, searchTerm, _httpClient);
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(results, JsonHelper.Default);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                var json = JsonSerializer.Serialize(ex.Message, JsonHelper.Default);
                await context.Response.WriteAsync(json);
            }
        }


        public static async Task OnInspectFile(HttpContext context, IQueryManager Query)
        {
            try
            {
                var files = await AnkiParser.Parser.ListFiles(context.Request.Query["anki"], Query);
                var cards = await AnkiParser.Parser.ParseCards(context.Request.Query["anki"], Query);
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new Inspection()
                {
                    Files = files,
                    Cards = cards
                }, JsonHelper.Default);
                await context.Response.WriteAsync(json);

            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                var json = JsonSerializer.Serialize(ex.Message, JsonHelper.Default);
                await context.Response.WriteAsync(json);
            }
        }

#endif

        public async Task<IEnumerable<DataLayer.Entities.File>?> Search(string? term)
        {
            if (_httpClient == null)
            {
                return [];
            }
            return await SearchAnki("127.0.0.1", term, _httpClient);
        }

        public async Task<IEnumerable<DataLayer.Entities.File>?> Download(string? ankiPackageUrl)
        {
            if (string.IsNullOrWhiteSpace(ankiPackageUrl))
            {
                throw new InvalidOperationException("Must enter a package name.");
            }


            // ResponseHeadersRead ensures we don't buffer the whole file into RAM first
            var response = await _httpClient.GetAsync(ankiPackageUrl, HttpCompletionOption.ResponseHeadersRead);
            _ = response.EnsureSuccessStatusCode();

            using var remoteStream = await response.Content.ReadAsStreamAsync();

            // We pass the stream directly to our generalized upload function
            // Using the URL's filename as the local path hint
            var fileName = Path.GetFileName(new Uri(ankiPackageUrl).LocalPath);
            Task? task = FileManager.UploadFile(remoteStream, fileName, "AnkiDownloads");
            if (task is Task wait) await wait;

            // Return the entity (you'll likely want to fetch the record created in UploadFile)
            Task<List<DataLayer.Entities.File>>? syncTask = Query.Synchronize<DataLayer.Entities.File>(f => f.Source == "Upload" || f.Source == "AnkiDownloads");
            if (syncTask is Task wait2) await wait2;
            return syncTask?.Result;
        }


        public class Inspection
        {
            public List<DataLayer.Entities.File>? Files { get; set; }
            public List<Card>? Cards { get; set; }

        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\d{8,12}")]
        private static partial System.Text.RegularExpressions.Regex AnkiIdRegex();
    }
}
