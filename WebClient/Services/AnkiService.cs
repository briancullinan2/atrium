using DataLayer.Entities;
using FlashCard.Services;
using System.Net.Http.Json;

namespace WebClient.Services
{
    public class AnkiService : IAnkiService
    {
        internal static IServiceProvider? _service;
        private readonly HttpClient _httpClient;

        public AnkiService()
        {
            if (_service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            _httpClient = _service.GetRequiredService<HttpClient>();
        }

        public async Task<IEnumerable<DataLayer.Entities.File>?> Download(string? ankiPackage)
        {
            if (string.IsNullOrWhiteSpace(ankiPackage))
            {
                throw new InvalidOperationException("Must enter a package name.");
            }
            var response = await _httpClient.PostAsync("api/download?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<List<DataLayer.Entities.File>>();
            return result;
        }

        public async Task<Tuple<IEnumerable<DataLayer.Entities.File>?, IEnumerable<Card>?>> InspectFile(string? ankiPackage)
        {
            var response = await _httpClient.PostAsync("api/inspect?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));

            var result = await response.Content.ReadFromJsonAsync<Inspection>();
            if (result == null)
            {
                return new Tuple<IEnumerable<DataLayer.Entities.File>?, IEnumerable<Card>?>(null, null);
            }
            return new Tuple<IEnumerable<DataLayer.Entities.File>?, IEnumerable<Card>?>(result.Files, result.Cards);
        }

        public async Task<IEnumerable<DataLayer.Entities.File>?> Search(string? term)
        {
            var response = await _httpClient.PostAsync("api/search?term=" + term, new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<List<DataLayer.Entities.File>>();
            return result;
        }
    }
}
