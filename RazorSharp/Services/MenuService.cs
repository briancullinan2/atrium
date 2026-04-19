using System.ComponentModel.DataAnnotations;

namespace RazorSharp.Services;



public class MenuService(ICompositeProvider Service, ITrustProvider Trust) : IMenuService
{

    // TODO: make this a static interface on IHasMenu to make it ask for types up front
    public List<Type> Menus
    {
        get => [.. new List<Type> { typeof(Layout.NavMenu) } // make our menu first
        .Concat(Trust.LoadedAssemblies.Values
        //.GetAssemblies()
        .GetMine().ToMenus())
        .Distinct()];
    }


    static Dictionary<Type, DisplayAttribute>? CachedPotentialRoutes { get; set; } = null;
    public Dictionary<Type, DisplayAttribute> PotentialRoutes
    {
        get => CachedPotentialRoutes
            ??= Trust.LoadedAssemblies.Values
            .GetMine()
            .SelectMany(TypeExtensions.GetAssTypesSafely)
            .Where(t => t.GetCustomAttributes() is IEnumerable<Attribute> attrs
                && attrs.Any(TypeExtensions.StaticMatchRouteAttribute)
                && attrs.Any(attr => attr is DisplayAttribute display
                    && !string.IsNullOrWhiteSpace(display.GroupName)))
            .ToDictionary(r => r!, r => r!.GetCustomAttributes<DisplayAttribute>().First());
    }


    public List<KeyValuePair<string, string>> Categories { get => [
        .. PotentialRoutes
        .Where(r => !string.IsNullOrWhiteSpace(r.Value.Prompt)) // get icons from Prompt field
        .Select(r => KeyValuePair.Create<string, string>(r.Value.GroupName ?? string.Empty, r.Value.Prompt ?? string.Empty))]; }


    public List<INavMenuItem> GetMenuItems(string menu) => [
        .. PotentialRoutes
        .Where(r => string.Equals(r.Value.GroupName, menu, StringComparison.InvariantCultureIgnoreCase))
        .Select(r => {
            var pageTyped = typeof(NavMenuItem<>).MakeGenericType(r.Key);
            var navItem = Activator.CreateInstance(pageTyped) as INavMenuItem 
                ?? throw new InvalidOperationException("Failed to create menu entry: " + r.Key);
            navItem.Title = r.Value.ShortName ?? r.Value.Name 
                ?? throw new InvalidOperationException("Menu title must be set through [Display(Name, ShortName)]: " + r.Key);
            navItem.Icon = r.Value.Prompt ?? string.Empty;
            if (r.Value.Name != null)
                navItem.Children = GetMenuItems(r.Value.Name);
            if (r.Value.ShortName != null && r.Value.ShortName != r.Value.Name)
                navItem.Children = [..navItem.Children, ..GetMenuItems(r.Value.ShortName)];
            return navItem;
        })
        .OfType<INavMenuItem>()];

    public List<Type> EnabledMenus { get => GetEnabledMenus(Service); }

    public List<Type> GetEnabledMenus(ICompositeProvider service) => [.. Menus.Where(m =>
    {
        var myDelegate = m.GetProperties(nameof(IHasMenu.ShowMenu)).First().GetValue(null) as Delegate;
        if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType)
            ?? myDelegate?.Method.ReturnType)?.Extends(typeof(bool)) != true)
            throw new InvalidOperationException("IHasMenu.ShowMenu delegate must return a bool" + myDelegate?.Method);
        return (bool?)myDelegate.InvokeService(service) == true;
    })];


    public static List<Type> Layouts { get; } = [.. (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetAssemblies().ToLayouts()];

    public List<Type> EnabledLayouts { get; private set; } = GetEnabledLayouts(Service);


    public static List<Type> GetEnabledLayouts(ICompositeProvider service) => [.. Layouts.Where(m =>
    {
        var myDelegate = m.GetProperties(nameof(IHasLayout.ShowLayout)).First().GetValue(null) as Delegate;
        if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType)
            ?? myDelegate?.Method.ReturnType)?.Extends(typeof(bool)) != true)
            throw new InvalidOperationException("IHasLayout.ShowLayout delegate must return a bool" + myDelegate?.Method);
        return (bool?)myDelegate.InvokeService(service) == true;
    })];

    public event Action<RenderFragment?>? OnMenuChanged;

    public async Task SetMenu(RenderFragment? menu)
    {
        OnMenuChanged?.Invoke(menu);
    }

    public event Action<bool?>? OnHeaderChanged;

    public async Task SetHeader(bool? show)
    {
        OnHeaderChanged?.Invoke(show);
    }


    // TODO: make this a static interface on IHasContext to make it ask for types up front
    static List<Type>? StoredContexts = null;
    public List<Type> Contexts { get => StoredContexts ??= Trust?.LoadedAssemblies.Values.GetMine().ToContexts().Distinct().ToList() ?? []; }


    private List<Type>? CachedEnabledContexts = null;
    public List<Type> EnabledContexts { get => CachedEnabledContexts ?? GetEnabledContexts(Service); private set => CachedEnabledContexts = value; }



    public  List<Type> GetEnabledContexts(ICompositeProvider service) => [.. Contexts.Where(m =>
    {
        var myDelegate = m.GetProperties(nameof(IHasContext.ShowContext)).First().GetValue(null) as Delegate;
        if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType)
            ?? myDelegate?.Method.ReturnType)?.Extends(typeof(bool)) != true)
            throw new InvalidOperationException("IHasContext.ShowContext delegate must return a bool" + myDelegate?.Method);
        return (bool?)myDelegate.InvokeService(service) == true;
    })];


}
