
using Microsoft.AspNetCore.Components.Routing;

namespace RazorSharp.Services;

public class NavigationTracker(IQueryManager query, AuthenticationStateProvider auth) : IDisposable
{
    private NavigationManager? nav;

    public void InitializeTracker(NavigationManager navManager)
    {
        nav = navManager;
        //nav = service.GetRequiredService<NavigationManager>();
        nav.LocationChanged += OnLocationChanged;
        OnLocationChanged(null, new LocationChangedEventArgs(nav.Uri, false));
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try
        {
            var state = await auth.GetAuthenticationStateAsync();
            var user = state.User;

            // Extract the SessionId from the Claims or Preferences
            var sessionId = user.FindFirst("SessionId")?.Value;
            //var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var uri = new Uri(e.Location);

            //query.Create<Visit>();
            var visit = new Visit
            {
                Path = uri.AbsolutePath,
                Query = uri.Query, // JSON string or raw
                Method = "GET",    // Blazor nav is always GET
                SessionId = sessionId,
                //UserId = userId,
                //Ip = 0, // TODO: inject this server side for web clients
                Hash = uri.Fragment.StartsWith('#') ? uri.Fragment[1..] : uri.Fragment, // Your fingerprinting logic
            };

            await query.Save(visit);
        }
        catch (Exception ex)
        {
            // Fail silently so we don't crash the UI on a failed log
            Console.WriteLine($"Visit Log Failed: {ex.Message}", ex);

        }
    }

    public void Dispose()
    {
        nav?.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
