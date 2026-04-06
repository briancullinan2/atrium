

using Extensions.SlenderServices;
using System.ComponentModel.DataAnnotations;

namespace RazorSharp.Services
{
    public interface IMenuService
    {
        Task SetMenu(RenderFragment? menu);
        event Action<RenderFragment?>? OnMenuChanged;

        Task SetHeader(bool? show);
        event Action<bool?>? OnHeaderChanged;

        List<Type> EnabledMenus { get; }
    }

    public class MenuService : IMenuService
    {

        // TODO: make this a static interface on IHasMenu to make it ask for types up front
        public static List<Type> Menus { get; } = [.. new List<Type> { typeof(Layout.NavMenu) } // make our menu first
            .Concat((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetAssemblies().ToMenus())
            .Distinct()];

        public static List<MethodInfo> AllRoutes { get; } = [.. (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetAssemblies().Routes()
            .Distinct()];


        public static Dictionary<Type, DisplayAttribute> PotentialRoutes { get; } = AllRoutes
            .Select(r => r.DeclaringType)
            .Distinct()
            .Where(r => r?.GetCustomAttribute<DisplayAttribute>() is DisplayAttribute attr
                && !string.IsNullOrWhiteSpace(attr.GroupName))
            .ToDictionary(r => r!, r => r!.GetCustomAttribute<DisplayAttribute>()!);


        public static List<KeyValuePair<string, string>> Categories { get; } = [.. PotentialRoutes
            .Where(r => !string.IsNullOrWhiteSpace(r.Value.Prompt)) // get icons from Prompt field
            .Select(r => KeyValuePair.Create<string, string>(r.Value.GroupName ?? string.Empty, r.Value.Prompt ?? string.Empty))];


        public static List<INavMenuItem> GetMenuItems(string menu) => [.. PotentialRoutes
            .Where(r => string.Equals(r.Value.GroupName, menu, StringComparison.InvariantCultureIgnoreCase))
            .Select(r => {
                var pageTyped = typeof(NavMenuItem<>).MakeGenericType(r.Key);
                var navItem = Activator.CreateInstance(pageTyped) as INavMenuItem;
                navItem?.Title = r.Value.ShortName ?? r.Value.Name ?? string.Empty;
                navItem?.Icon = r.Value.Prompt ?? string.Empty;
                if (navItem?.Title != null)
                    navItem?.Children = GetMenuItems(navItem?.Title!);
                return navItem;
            })
            .OfType<INavMenuItem>()];

        public List<Type> EnabledMenus { get; private set; } = [];

        public MenuService(IServiceProvider Service)
        {
            EnabledMenus = GetEnabledMenus(Service);
        }


        public static List<Type> GetEnabledMenus(IServiceProvider service) => [.. Menus.Where(m =>
        {
            var myDelegate = m.GetProperties(nameof(IHasMenu.ShowMenu)).First().GetValue(null) as Delegate;
            return myDelegate.InvokeService(service);
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
    }
}
