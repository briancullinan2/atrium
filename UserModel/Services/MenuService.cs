namespace UserModel.Services;

public class MenuService : IHasContext
{
    public static Delegate ShowContext
    {
        get => (Type routeControl, NavigationManager Nav) => routeControl == typeof(Pages.Admin.Activity)
            || routeControl == typeof(Pages.Admin.Roles) || routeControl == typeof(Pages.Admin.Groups)
            || routeControl == typeof(Pages.Admin.Users) || routeControl == typeof(Pages.Landing.Account)
            || routeControl == typeof(Pages.Admin.Settings) || routeControl == typeof(Pages.Admin.Providers)
            || routeControl == typeof(Pages.Admin.Permissions)
            || Nav.Uri.Contains("/status", StringComparison.InvariantCultureIgnoreCase);
    }


    // TODO: convert this to a utility next to GetUri(this TComponent) and automatically fill in with attributes, least repetative
    public static Delegate ContextInsert => (Func<Type, NavigationManager, RenderFragment>)(
        (Type routeControl, NavigationManager Nav) => (__builder) =>
        {
            if (Nav.Uri.Contains("/status", StringComparison.InvariantCultureIgnoreCase))
            {
                RenderExtensions.ToNavLink<Pages.Admin.Activity>()(__builder);
                return;
            }
            if(routeControl == typeof(Pages.Admin.Settings)
                || routeControl == typeof(Pages.Landing.Account)
                || routeControl == typeof(Pages.Admin.Permissions))
            {
                RenderExtensions.ToNavLink<Pages.Landing.Account>()(__builder);
                RenderExtensions.ToNavLink<Pages.Admin.Settings>(s => new() { Mode = ControlMode.List }, "Theme Settings")(__builder);
                RenderExtensions.ToNavLink<Pages.Admin.Providers>()(__builder);
            }


            {
                RenderExtensions.ToNavLink<Pages.Admin.Users>(u => new() { Mode = ControlMode.Add }, "Add User", "bi-person-plus")(__builder);
                RenderExtensions.ToNavLink<Pages.Admin.Groups>()(__builder);
                //RenderExtensions.ToNavLink<Pages.Admin.Roles>()(__builder);
                RenderExtensions.ToNavLink<Pages.Admin.Activity>()(__builder);
            }
        }
    );
}
