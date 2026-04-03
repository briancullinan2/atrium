using System.Text.Json.Serialization;

namespace Hosting.Services
{
    public interface IHostingService
    {
        Task<string?> GetToken();
        Task<string?> GetHost();
        Task<string?> CheckTunnel(string? Account = null, string? Tunnel = null, string? Api = null);
        Task<bool?> CheckInstalled();
        Task<bool?> IsWorking();
        Task<StatusResponse?> CheckStatus(string? domain);
        event Action<bool?>? OnHttpWorking;
    }


    public class HostingSettings
    {
        public string? AccountId { get; set; }
        public string? ApiToken { get; set; }
        public string? TunnelName { get; set; }
        public string? Domain { get; set; }
    }


    public class StatusResponse
    {
        [JsonIgnore]
        private static string _guid;
        [JsonIgnore]
        private static DateTime _now;
        static StatusResponse()
        {
            _guid = Guid.NewGuid().ToString();
            _now = DateTime.Now;
        }
        public DateTime? Now { get; set; }
        public static List<string>? ItWorks { get => [_guid]; set => _guid = value?.Count > 0 ? value?.ElementAt(0) ?? _guid : _guid; }
        public string? Host { get; set; }
        public string? Tunnel { get; set; }
        public bool? Installed { get; set; }
        public string? Error { get; set; }
        public StatusResponse()
        {
            Now = DateTime.Now;
            if (_now + TimeSpan.FromMinutes(2) < Now)
            {
                _guid = Guid.NewGuid().ToString();
                _now = DateTime.Now;
            }
        }
    }

}
