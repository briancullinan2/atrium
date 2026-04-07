namespace Interfacing.Services;

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

    public StatusResponse()
    {
        if (Now < DateTime.Now.AddMinutes(2))
        {
            StableGuid = Guid.NewGuid().ToString();
            Now = DateTime.Now;
        }
    }

    [JsonIgnore]
    private static string StableGuid { get; set; } = Guid.NewGuid().ToString();
    public static DateTime? Now { get; set; } = DateTime.Now;

    public static List<string>? ItWorks { get => [StableGuid]; set => StableGuid = value?.Count > 0 ? value?.ElementAt(0) ?? StableGuid : StableGuid; }
    public string? Host { get; set; }
    public string? Tunnel { get; set; }
    public object? Error { get; set; }
}
