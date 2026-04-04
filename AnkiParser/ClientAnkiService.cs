using System.Net.Http.Json;

namespace AnkiParser
{
    public class AnkiService : IAnkiService
    {
        private static HttpClient? _httpClient;

        public AnkiService(HttpClient client)
        {
            _httpClient ??= client;
        }

        public async Task<IEnumerable<File>?> Download(string? ankiPackage)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("No http client.");
            }
            if (string.IsNullOrWhiteSpace(ankiPackage))
            {
                throw new InvalidOperationException("Must enter a package name.");
            }
            var response = await _httpClient.PostAsync("api/download?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<List<File>>();
            return result;
        }

        public async Task<Tuple<IEnumerable<File>?, IEnumerable<Card>?>> InspectFile(string? ankiPackage)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("No http client.");
            }
            var response = await _httpClient.PostAsync("api/inspect?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));

            var result = await response.Content.ReadFromJsonAsync<Inspection>();
            if (result == null)
            {
                return new Tuple<IEnumerable<File>?, IEnumerable<Card>?>(null, null);
            }
            return new Tuple<IEnumerable<File>?, IEnumerable<Card>?>(result.Files, result.Cards);
        }

        public async Task<IEnumerable<File>?> Search(string? term)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("No http client.");
            }
            var response = await _httpClient.PostAsync("api/search?term=" + term, new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<List<File>>();
            return result;
        }
    }
}
