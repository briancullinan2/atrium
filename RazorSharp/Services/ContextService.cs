

namespace RazorSharp.Services;

// TODO: see ContextMenu.razor for a .razor example, works exactly the same
internal class ContextService : IHasContext
{
    public static Delegate ShowContext
    {
        get => (Type routeControl, NavigationManager Nav) => Nav.Uri.Contains("/admin", StringComparison.InvariantCultureIgnoreCase);
    }


    // TODO: convert this to a utility next to GetUri(this TComponent) and automatically fill in with attributes, least repetative
    public static Delegate ContextInsert => (Func<Type, NavigationManager, RenderFragment>)(
        (Type routeControl, NavigationManager Nav) => (__builder) =>
        {
            if (!routeControl.Extends(typeof(INotHasWrapper))
                && !Nav.Uri.Contains("/login", StringComparison.InvariantCultureIgnoreCase))
            {
                TypeExtensions.ToNavLink<Pages.Landing.Search>()(__builder);
            }
            if (Nav.Uri.Contains("/admin", StringComparison.InvariantCultureIgnoreCase))
            {
                TypeExtensions.ToNavLink<Pages.Admin.Status>()(__builder);
            }
            
        }
    );
}
