


using System.ComponentModel.DataAnnotations;

namespace RazorSharp.Services;

// TODO: see ContextMenu.razor for a .razor example, works exactly the same
internal class ContextService : IHasContext
{
    public static Func<Type?, bool> NotHomePageOrLogin = routeControl 
        => !routeControl.Extends(typeof(INotHasWrapper))
            && routeControl?.GetType().FullName?.Contains("login", StringComparison.InvariantCultureIgnoreCase) != true;

    public static Func<Type?, bool> IsAdminPage = routeControl 
        => routeControl?.GetType().Namespace?.Contains("admin", StringComparison.InvariantCultureIgnoreCase) == true
        || routeControl?.Route()?.Contains("admin") == true
        || routeControl?.GetCustomAttributes<DisplayAttribute>().FirstOrDefault()?.GroupName?.Contains("admin", StringComparison.InvariantCultureIgnoreCase) == true;

    public static Delegate ShowContext
    {
        get => (Type? routeControl) => NotHomePageOrLogin(routeControl) || IsAdminPage(routeControl);
    }


    // TODO: convert this to a utility next to GetUri(this TComponent) and automatically fill in with attributes, least repetative
    public static Delegate ContextInsert => (Func<Type?, RenderFragment>)(
        (routeControl) => (__builder) =>
        {
            if (NotHomePageOrLogin(routeControl))
            {
                RenderExtensions.ToNavLink<Pages.Landing.Search>()(__builder);
            }
            if (IsAdminPage(routeControl))
            {
                RenderExtensions.ToNavLink<Pages.Admin.Status>()(__builder);
            }
            
        }
    );
}
