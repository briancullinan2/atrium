using System.Text.Json.Serialization;

namespace FlashCard.Services
{
    public interface IStatusService
    {
        Task<string?> GetToken();
        Task<string?> GetHost();
        Task<string?> CheckTunnel(string? _account = null, string? _tunnel = null, string? _api = null);
        Task<bool?> CheckInstalled();
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
        private static DateTime _now;
        static StatusResponse()
        {
            _guid = System.Guid.NewGuid().ToString();
            _now = DateTime.Now;
        }
        public DateTime? Now { get; set; }
        public List<string>? ItWorks { get => [StatusResponse._guid]; }
        public string? Host { get; set; }
        public string? Tunnel { get; set; }
        public bool? Installed { get; set; }
        public StatusResponse()
        {
            Now = DateTime.Now;
            if (_now + TimeSpan.FromMinutes(2) < Now)
            {
                _guid = System.Guid.NewGuid().ToString();
                _now = DateTime.Now;
            }
        }
    }

}
