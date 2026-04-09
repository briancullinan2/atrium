

namespace FlashCard.Services;

// TODO: see ContextMenu.razor for a .razor example, works exactly the same
internal class ContextService : IHasContext
{
    public static Delegate ShowContext
    {
        get => (Type routeControl, NavigationManager Nav) => Nav.Uri.Contains("/upload", StringComparison.InvariantCultureIgnoreCase)
            || routeControl == typeof(Pages.Landing.Courses) || routeControl == typeof(Pages.Landing.EditPack)
            || routeControl == typeof(Pages.Landing.Packs);
    }


    // TODO: convert this to a utility next to GetUri(this TComponent) and automatically fill in with attributes, least repetative
    public static Delegate ContextInsert => (Func<Type, NavigationManager, RenderFragment>)(
        (Type routeControl, NavigationManager Nav) => (__builder) =>
        {
            //if (Nav.Uri.Contains("/upload", StringComparison.InvariantCultureIgnoreCase))
            {
                RenderExtensions.ToNavLink<Pages.Landing.EditPack>(p => new() { Mode = ControlMode.Edit }, "Create Pack", "bi-file-earmark-plus");
            }
            if (!Nav.Uri.Contains("/upload", StringComparison.InvariantCultureIgnoreCase))
            {
                RenderExtensions.ToNavLink<Pages.Landing.Packs>(p => new() { Mode = ControlMode.Owner }, "My Packs", "bi-file-earmark-text");
                RenderExtensions.ToNavLink<Pages.Landing.Packs>(p => new() { Filter = "Purchased" }, "Purchases", "bi-shield-lock");
                RenderExtensions.ToNavLink<Pages.Landing.Packs>(p => new() { Filter = "Subscribe" }, "Subscribe", "bi-envelope-at");
            }

        }
    );
}
