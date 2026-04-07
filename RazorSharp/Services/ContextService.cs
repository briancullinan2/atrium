
namespace RazorSharp.Services
{
    // TODO: see ContextMenu.razor for a .razor example, works exactly the same
    internal class ContextService : IHasContext
    {
        public static Delegate ShowContext
        {
            get => (Type routeControl, NavigationManager Nav) => Nav.Uri.Contains("/admin", StringComparison.InvariantCultureIgnoreCase);
        }

    
        // TODO: convert this to a utility next to GetUri(this TComponent) and automatically fill in with attributes, least repetative
        public static Delegate ContextInsert => (Func<NavigationManager, RenderFragment>)(
            (NavigationManager Nav) => (__builder) =>
            {
                if (Nav.Uri.Contains("/admin", StringComparison.InvariantCultureIgnoreCase))
                {
                    TypeExtensions.ToNavLink<Pages.Landing.Search>()(__builder);
                    TypeExtensions.ToNavLink<Pages.Admin.Status>()(__builder);
                }
                
            }
        );
    }
}
