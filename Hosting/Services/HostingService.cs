#if !BROWSER
using Microsoft.AspNetCore.Http;
#endif
#if WINDOWS
using System.ServiceProcess;
#endif

namespace Hosting.Services;

internal class HostingService(HttpClient Http) : IHostingService
{
    private static readonly string CredentialsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".credentials");
    private static readonly string SettingsPath = Path.Combine(CredentialsDir, "atrium-hosting.json");
    private static readonly long[] _versionCache;
    private static HostingSettings? _settings;

    private StatusResponse? _recentResult;
    private DateTime _lastChecked = DateTime.MinValue;

    public event Action<bool?>? OnHttpWorking;

    static HostingService()
    {
        if (File.Exists(SettingsPath))
        {
            try { _settings = JsonSerializer.Deserialize<HostingSettings>(File.ReadAllText(SettingsPath)); }
            catch { /* Silent fail for corrupted JSON */ }
        }


        long appStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long latestFile = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Max(a => { try { return new FileInfo(a.Location).LastWriteTimeUtc; } catch { return DateTime.MinValue; } })
            .Ticks / 10000;

        _versionCache = [appStart, latestFile];

    }


    // --- IHostingService Implementation ---

    public async Task<string?> GetToken() => StatusResponse.ItWorks?[0];

    public async Task<string?> GetHost() => _settings?.Domain ?? _recentResult?.Host;

    /// <summary>
    /// One-way save: Updates local settings and persists them to the user profile.
    /// Useful for setting Cloudflare IDs from the client UI.
    /// </summary>
    public static async Task SaveSettings(HostingSettings newSettings)
    {
        _settings = newSettings;
        if (!Directory.Exists(CredentialsDir)) Directory.CreateDirectory(CredentialsDir);
        await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(_settings));
    }

    public async Task<bool?> CheckInstalled()
    {
#if WINDOWS
        try
        {
            return ServiceController.GetServices().Any(s => s.ServiceName.Equals("Cloudflared", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
#else
        return false;
#endif
    }

    public async Task<bool?> IsWorking()
    {
        var host = await GetHost();
        if (string.IsNullOrEmpty(host)) return false;

        var result = await CheckStatus(host);
        bool isWorking = result?.Error == null && result?.Tunnel == "healthy";

        OnHttpWorking?.Invoke(isWorking);
        return isWorking;
    }

    /// <summary>
    /// Probes a remote domain for its status.
    /// </summary>
    public async Task<StatusResponse?> CheckStatus(string? domain)
    {
        if (string.IsNullOrEmpty(domain)) return null;

        // 2-minute cache rule
        if (_recentResult != null && _lastChecked.AddMinutes(2) > DateTime.Now && _recentResult.Host == domain)
            return _recentResult;

        try
        {
            var url = $"https://{domain.Replace("https://", "")}/api/status";
            var response = await Http.PostAsJsonAsync(url, _settings);
            _recentResult = await response.Content.ReadFromJsonAsync<StatusResponse>();
            _lastChecked = DateTime.Now;
            return _recentResult;
        }
        catch (Exception ex)
        {
            return new StatusResponse { Error = ex.Message, Host = domain };
        }
    }

    /// <summary>
    /// Checks Cloudflare API for tunnel health.
    /// </summary>
    public async Task<string?> CheckTunnel(string? account = null, string? tunnel = null, string? api = null)
    {
        var acc = account ?? _settings?.AccountId;
        var tun = tunnel ?? _settings?.TunnelName;
        var tok = api ?? _settings?.ApiToken;

        if (string.IsNullOrWhiteSpace(acc) || string.IsNullOrWhiteSpace(tun) || string.IsNullOrWhiteSpace(tok))
            return "Missing Credentials";

        try
        {
            Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
            var url = $"https://api.cloudflare.com/client/v4/accounts/{acc}/cfd_tunnel?name={tun}&is_deleted=false";

            var cfResponse = await Http.GetFromJsonAsync<CloudflareResponse>(url);
            return cfResponse?.Result?.FirstOrDefault()?.Status ?? "Unknown";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    // TODO: check versions from clients using full circuit?

#if !BROWSER
    public static async Task OnVersionCheck(HttpContext context)
    {
        await context.Response.WriteAsJsonAsync(_versionCache);
    }

    public static async Task OnStatusCheck(HttpContext context, IHostingService service)
    {
        var inputSettings = await context.Request.ReadFromJsonAsync<HostingSettings>();
        var result = await service.CheckTunnel(inputSettings?.AccountId, inputSettings?.TunnelName, inputSettings?.ApiToken);
        await context.Response.WriteAsJsonAsync(result);
    }
#endif
}


public class CloudflareResponse { public List<TunnelInfo>? Result { get; set; } }
public class TunnelInfo { public string? Status { get; set; } }