namespace AnkiParser;

public partial class AnkiService
{

    protected static async Task<IEnumerable<File>?> DoActualSearch(string? searchTerm, HttpClient? HttpClient)
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
            var files = new List<File>();

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
                        files.Add(new File
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


}
