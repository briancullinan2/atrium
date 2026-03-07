using DataLayer.Entities;
using StudySauce.Shared.Services;
using System.Net.Http.Json;

namespace StudySauce.Web.Client.Services
{
    public class AnkiService : IAnkiService
    {
        internal static IServiceProvider? _service;
        private readonly HttpClient _httpClient;

        public AnkiService()
        {
            _httpClient = _service.GetRequiredService<HttpClient>();
        }

        public async Task<IEnumerable<DataLayer.Entities.File>> Download(string ankiPackage)
        {
            var response = await _httpClient.PostAsync("api/download?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<List<DataLayer.Entities.File>>();
            return result;
        }

        public async Task<Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<Card>>> InspectFile(string ankiPackage)
        {
            var response = await _httpClient.PostAsync("api/inspect?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));

            var result = await response.Content.ReadFromJsonAsync<Inspection>();
            return new Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<Card>>(result.Files, result.Cards);
        }

        public async Task<IEnumerable<DataLayer.Entities.File>?> Search(string? term)
        {
            var response = await _httpClient.PostAsync("api/search?term=" + term, new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<List<DataLayer.Entities.File>>();
            return result;
        }
    }
}
