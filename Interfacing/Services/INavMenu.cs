using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Text;

namespace Interfacing.Services;

public interface IMenuService
{
    //Task SetMenu(RenderFragment? menu);
    //event Action<RenderFragment?>? OnMenuChanged;

    //Task SetHeader(bool? show);
    //event Action<bool?>? OnHeaderChanged;

    List<Type> EnabledMenus { get; }
    List<Type> EnabledContexts { get; }
    List<Type> EnabledLayouts { get; }
}

public interface IHasMenu
{
    static abstract Delegate ShowMenu { get; }
    Task SetMenuActivated(bool menu);
    static abstract string Icon { get; }
}


public interface IHasLayout
{
    static abstract Delegate ShowLayout { get; }
    static abstract Delegate LayoutInsert { get; }
}


public interface IHasContext 
{
    static abstract Delegate ShowContext { get; }
    static abstract Delegate ContextInsert { get; }
}

public interface INotHasWrapper
{

}

public interface IHasPageContext
{
    static abstract Delegate ShowPageContext { get; }
    static abstract Delegate PageContextInsert { get; }
}


public interface IRenderService
{
    Action<object> ChildContent { get; }
}


public interface ILogoService : IRenderService
{
}

public interface IHasAccordion
{

}


public interface IHasCards
{

}

public interface IHasTransition
{

}

public interface IRenderLinks : IRenderService
{
    void Register(Type who, string path);
    List<string> Registry { get; }
}

public interface IJavascriptOutlet : IRenderLinks
{

}

public interface ICssOutlet : IRenderLinks
{

}


public partial class RenderService(ICompositeProvider service) : IRenderService
{
    protected ICompositeProvider Service { get; set; } = service;

    public virtual Action<object> ChildContent
    {
        get
        {
            return ((object __builder) => {
                (GetType().GetProperty("Current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as Delegate)?.DynamicInvoke(__builder);
            });
        }
        set
        { }
    }

    public static implicit operator Delegate(RenderService service)
    {
        return service.ChildContent ?? (Delegate)((object __builder) => { });
    }

}


public abstract partial class RenderOutlet : RenderService, IRenderLinks, IDisposable
{
    protected readonly ConcurrentDictionary<Type, List<string>> RealRegistry = [];
    private IHasClass? Main;

    //private readonly NavigationManager Nav;
    private readonly ITrustProvider Trust;
    private bool IsClosing;

    public List<string> Registry { get => [.. RealRegistry.SelectMany(list => list.Value).Distinct()]; }
    public event Action? OnChanged;

    public override Action<object> ChildContent
    {
        get => ((object __builder) => (__builder as dynamic).AddMarkupContent(0, BuildRenderTree())); set => base.ChildContent = value;
    }


    public void Register(Type who, string path)
    {
        if (RealRegistry.TryGetValue(who, out var list))
        {
            list.Add(path);
        }
        else
            RealRegistry.TryAdd(who.GetType(), [path]);
        OnChanged?.Invoke();
    }


    public RenderOutlet(ICompositeProvider Service, ITrustProvider _trust)
        : base(Service)
    {
        //Nav = _nav;
        Trust = _trust;
        Trust.OnSettledAsync += ListenForNeeds;
    }


    protected abstract List<string> TypeToIncludes(Type type);
    protected abstract string BuildRenderTree();

    protected virtual async Task ListenForNeeds()
    {
        if (IsClosing) return;

        Trust.OnSettledAsync += ListenForNeeds; // resubscribe for the next lazy event

        await Task.Delay(1000); // wait for layout to insert the component

        if (IsClosing) return;

        Main ??= Service.GetService<IHasClass>();
        var components = Main?.GetChildComponents().Select(c => c.GetType()) ?? [];

        foreach (var type in components)
        {
            var includes = type.GetInterfaces().Append(type).SelectMany(TypeToIncludes).ToList();

            if (RealRegistry.TryGetValue(type, out var list)) list.AddRange(includes);
            else RealRegistry[type] = includes;
        }


        foreach (var component in RealRegistry)
        {
            if (!components.Contains(component.Key))
            {
                RealRegistry.TryRemove(component);
            }
        }

        Main?.HasChanged();
    }

    public void Dispose()
    {
        IsClosing = true;
        GC.SuppressFinalize(this);
    }
}

public partial class CssOutlet(ICompositeProvider Service, ITrustProvider Trust) 
    : RenderOutlet(Service, Trust), ICssOutlet
{
    private readonly Dictionary<string, string> _filePresence = [];

    protected override List<string> TypeToIncludes(Type type) => type switch
    {
        _ when type == typeof(IHasForms) => ["_content/RazorSharp/css/accordion.css", "_content/RazorSharp/css/forms.css"],
        _ when type.Name.Contains("Layout", StringComparison.InvariantCultureIgnoreCase) => ["_content/RazorSharp/css/main.css"],
        _ => []
    };



    protected override async Task ListenForNeeds()
    {
        await base.ListenForNeeds();


        foreach (var style in RealRegistry)
        {
            var names = style.Key.Assembly.GetManifestResourceNames();
            var resFile = names.Where(f => f.Contains("forms"));
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var include in style.Value)
            {
                // Use the relative path logic we discussed
                var exists = File.Exists(Path.Combine(appPath, "wwwroot",  include));
                if (exists)
                {
                    var cssString = await File.ReadAllTextAsync(Path.Combine(appPath, "wwwroot", include));
                    _filePresence[include] = cssString;
                }
            }

        }

    }

    protected override string BuildRenderTree()
    {
        var sb = new StringBuilder();

        foreach (var style in Registry)
        {
            if (_filePresence.TryGetValue(style, out var cssString))
            {
                sb.Append("<style>")
                  .Append(cssString)
                  .AppendLine("</style>");
            }
            else
            {
                // Using an interpolated string for the link tag
                sb.AppendLine($"<link rel=\"stylesheet\" href=\"{style}\" />");
            }
        }

        return sb.ToString();
    }
}

public partial class JavascriptOutlet(ICompositeProvider Service, ITrustProvider Trust)
    : RenderOutlet(Service, Trust), IJavascriptOutlet
{
    protected override List<string> TypeToIncludes(Type type) => type switch
    {
        //    _ when type is IHasForms => ["_content/RazorSharp/css/forms.css"],
        _ => []
    };

    protected override string BuildRenderTree()
    {
        var sb = new StringBuilder();

        foreach (var style in Registry)
        {
            sb.AppendLine($"<script type=\"application/javascript\" src=\"{style}\" />");
        }

        return sb.ToString();
    }
    
}

