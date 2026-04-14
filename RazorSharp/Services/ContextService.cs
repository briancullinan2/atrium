


namespace RazorSharp.Services;

// TODO: see ContextMenu.razor for a .razor example, works exactly the same
internal class ContextService : IHasContext
{
    public static Delegate ShowContext
    {
        get => (Type routeControl, NavigationManager Nav)
            => true; //Nav.Uri.Contains("/admin", StringComparison.InvariantCultureIgnoreCase);
    }


    // TODO: convert this to a utility next to GetUri(this TComponent) and automatically fill in with attributes, least repetative
    public static Delegate ContextInsert => (Func<Type?, RenderFragment>)(
        (routeControl) => (__builder) =>
        {
            if (!routeControl.Extends(typeof(INotHasWrapper))
                && routeControl?.GetType().FullName?.Contains("login", StringComparison.InvariantCultureIgnoreCase)  == true)
            {
                RenderExtensions.ToNavLink<Pages.Landing.Search>()(__builder);
            }
            if (routeControl?.GetType().Namespace?.Contains("admin", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                RenderExtensions.ToNavLink<Pages.Admin.Status>()(__builder);
            }
            
        }
    );
}
