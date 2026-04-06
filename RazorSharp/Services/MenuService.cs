

using Extensions.SlenderServices;
using System.ComponentModel.DataAnnotations;

namespace RazorSharp.Services
{
   

    public class MenuService(IServiceProvider Service) : IMenuService
    {

        // TODO: make this a static interface on IHasMenu to make it ask for types up front
        public static List<Type> Menus { get; } = [.. new List<Type> { typeof(Layout.NavMenu) } // make our menu first
            .Concat((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetAssemblies().ToMenus())
            .Distinct()];


        public static Dictionary<Type, DisplayAttribute> PotentialRoutes { get; } = TypeExtensions.AllRoutes
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

        public List<Type> EnabledMenus { get; private set; } = GetEnabledMenus(Service);

        public static List<Type> GetEnabledMenus(IServiceProvider service) => [.. Menus.Where(m =>
        {
            var myDelegate = m.GetProperties(nameof(IHasMenu.ShowMenu)).First().GetValue(null) as Delegate;
            if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType) 
                ?? myDelegate?.Method.ReturnType)?.Extends(typeof(RenderFragment)) != true)
                throw new InvalidOperationException("Menu delegate must return a RenderFragment" + myDelegate?.Method);
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


        // TODO: make this a static interface on IHasContext to make it ask for types up front
        public static List<Type> Contexts { get; } = [.. new List<Type> { typeof(Layout.NavMenu) } // make our menu first
        .Concat((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetAssemblies().ToContexts())
        .Distinct()];

        public List<Type> EnabledContexts { get; private set; } = GetEnabledContexts(Service);



        public static List<Type> GetEnabledContexts(IServiceProvider service) => [.. Contexts.Where(m =>
        {
            var myDelegate = m.GetProperties(nameof(IHasContext.ShowContext)).First().GetValue(null) as Delegate;
            if(myDelegate == null || (Nullable.GetUnderlyingType(myDelegate.Method.ReturnType)
                ?? myDelegate?.Method.ReturnType)?.Extends(typeof(RenderFragment)) != true)
                throw new InvalidOperationException("Context delegate must return a RenderFragment" + myDelegate?.Method);
            return myDelegate.InvokeService(service);
        })];


    }
}
