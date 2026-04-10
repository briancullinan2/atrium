using System.ComponentModel.DataAnnotations;

namespace RazorSharp.Services;



public class MenuService(IServiceProvider Service) : IMenuService
{

    // TODO: make this a static interface on IHasMenu to make it ask for types up front
    public static List<Type> Menus { get; } = [.. new List<Type> { typeof(Layout.NavMenu) } // make our menu first
        .Concat((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetAssemblies().ToMenus())
        .Distinct()];



    public static Dictionary<Type, DisplayAttribute> PotentialRoutes { get; } = TypeExtensions.AllRoutable
        .Where(r => r?.GetCustomAttributes<DisplayAttribute>().FirstOrDefault() is DisplayAttribute attr
            && !string.IsNullOrWhiteSpace(attr.GroupName))
        .ToDictionary(r => r!, r => r!.GetCustomAttributes<DisplayAttribute>().First());


    public static List<KeyValuePair<string, string>> Categories { get; } = [.. PotentialRoutes
        .Where(r => !string.IsNullOrWhiteSpace(r.Value.Prompt)) // get icons from Prompt field
        .Select(r => KeyValuePair.Create<string, string>(r.Value.GroupName ?? string.Empty, r.Value.Prompt ?? string.Empty))];


    public static List<INavMenuItem> GetMenuItems(string menu) => [.. PotentialRoutes
        .Where(r => string.Equals(r.Value.GroupName, menu, StringComparison.InvariantCultureIgnoreCase))
        .Select(r => {
            var pageTyped = typeof(NavMenuItem<>).MakeGenericType(r.Key);
            var navItem = Activator.CreateInstance(pageTyped) as INavMenuItem ?? throw new InvalidOperationException("Failed to create menu entry: " + r.Key);
            navItem.Title = r.Value.ShortName ?? r.Value.Name ?? throw new InvalidOperationException("Menu title must be set through [Display(Name, ShortName)]: " + r.Key);
            navItem.Icon = r.Value.Prompt ?? string.Empty;
            if (r.Value.Name != null)
                navItem.Children = GetMenuItems(r.Value.Name);
            if (r.Value.ShortName != null && r.Value.ShortName != r.Value.Name)
                navItem.Children = [..navItem.Children.Concat(GetMenuItems(r.Value.ShortName))];
            return navItem;
        })
        .OfType<INavMenuItem>()];

    public List<Type> EnabledMenus { get; private set; } = GetEnabledMenus(Service);

    public static List<Type> GetEnabledMenus(IServiceProvider service) => [.. Menus.Where(m =>
    {
        var myDelegate = m.GetProperties(nameof(IHasMenu.ShowMenu)).First().GetValue(null) as Delegate;
        if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType) 
            ?? myDelegate?.Method.ReturnType)?.Extends(typeof(bool)) != true)
            throw new InvalidOperationException("IHasMenu.ShowMenu delegate must return a bool" + myDelegate?.Method);
        return (bool?)myDelegate.InvokeService(service) == true;
    })];


    public static List<Type> Layouts { get; } = [.. (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetAssemblies().ToLayouts()];

    public List<Type> EnabledLayouts { get; private set; } = GetEnabledLayouts(Service);


    public static List<Type> GetEnabledLayouts(IServiceProvider service) => [.. Layouts.Where(m =>
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
    public static List<Type> Contexts { get; } = [.. (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
        .GetAssemblies().ToContexts().Distinct()];

    public List<Type> EnabledContexts { get; private set; } = GetEnabledContexts(Service);



    public static List<Type> GetEnabledContexts(IServiceProvider service) => [.. Contexts.Where(m =>
    {
        var myDelegate = m.GetProperties(nameof(IHasContext.ShowContext)).First().GetValue(null) as Delegate;
        if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType)
            ?? myDelegate?.Method.ReturnType)?.Extends(typeof(bool)) != true)
            throw new InvalidOperationException("IHasContext.ShowContext delegate must return a bool" + myDelegate?.Method);
        return (bool?)myDelegate.InvokeService(service) == true;
    })];


}
