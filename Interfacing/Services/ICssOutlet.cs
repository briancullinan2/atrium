using System.Collections.Concurrent;
using System.Text;

namespace Interfacing.Services;

public interface IRenderService
{
    Action<object> ChildContent { get; }
}


public interface ILogoService : IRenderService
{
}

public interface IRenderLinks : IRenderService
{
    void Register(Type who, string path);
    Task ListenUp(Type? typeHint = null);
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
            return (__builder =>
            {
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

    //private readonly NavigationManager Nav;
    private readonly ITrustProvider Trust;
    private bool IsClosing;
    private readonly RenderStateProvider Rendered;

    public List<string> Registry { get => [.. RealRegistry.SelectMany(list => list.Value).Distinct()]; }
    public event Action? OnChanged;

    public override Action<object> ChildContent
    {
        get => __builder => (__builder as dynamic).AddMarkupContent(0, BuildRenderTree()); set => base.ChildContent = value;
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


    public RenderOutlet(ICompositeProvider Service, ITrustProvider _trust, RenderStateProvider rendered)
        : base(Service)
    {
        //Nav = _nav;
        Trust = _trust;
        Trust.OnSettledAsync += ListenTrust;
        Rendered = rendered;
        Rendered.OnRendered += ListenRendered;
    }


    protected abstract List<string> TypeToIncludes(Type type);
    protected abstract string BuildRenderTree();

    public virtual void ListenRendered()
    {
        if (IsClosing) return;

        _ = ListenUp();
    }


    public virtual async Task ListenTrust()
    {
        if (IsClosing) return;

        Trust.OnSettledAsync += ListenTrust; // resubscribe for the next lazy event

        await ListenUp();
    }

    Type? previousHint = null;

    public virtual async Task ListenUp(Type? typeHint = null)
    {
        if (IsClosing) return;

        await Task.Delay(800); // wait for layout to insert the component

        if (IsClosing) return;

        if (typeHint != null)
            previousHint = typeHint;
        var components = Rendered._container?.GetChildComponents()
            .Select(c => c.GetType()).Concat(previousHint != null ? [previousHint, Rendered._container.GetType()] : [Rendered._container.GetType()]) ?? [];

        foreach (var type in components)
        {

            var includes = type.GetInterfaces()
                .Concat(type.BaseType != null ? [type, type.BaseType] : [type])
                .SelectMany(TypeToIncludes)
                .ToList();

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

        if (Rendered?._container?.HasChanged() is Task task) await task;
    }

    public void Dispose()
    {
        IsClosing = true;
        Trust.OnSettledAsync -= ListenTrust;
        Rendered.OnRendered -= ListenRendered;
        GC.SuppressFinalize(this);
    }
}


public partial class CssOutlet
    : RenderOutlet, ICssOutlet, IHasClass
{
    private readonly Dictionary<string, string> _filePresence = [];

    protected override List<string> TypeToIncludes(Type type) => type switch
    {
        _ when type == typeof(IHasForms) => [
            "/_content/RazorSharp/css/accordion.css",
            "/_content/RazorSharp/css/forms.css"],
        _ when type == typeof(IHasAccordion) => [
            "/_content/RazorSharp/css/accordion.css"],
        _ when type == typeof(IHasCover) => ["/css/cover.css"],
        _ when type.Name.Contains("LayoutComponentBase") => [
            "/_content/RazorSharp/css/layout.css",
            "/_content/RazorSharp/css/menu.css",
            "/_content/RazorSharp/css/nav.css"],
        _ when type.Name.Contains("Layout", StringComparison.InvariantCultureIgnoreCase) => ["/_content/RazorSharp/css/main.css"],
        _ => []
    };


    public void SetUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Trim('/'))) PageClasses = ["Home"];
        else PageClasses = [..uri.Split('?')[0]
            .Split('/')
            .Select(seg => seg.ToSafe())
        ];
    }


    public CssOutlet(ICompositeProvider Service, ITrustProvider Trust, RenderStateProvider rendered)
        : base(Service, Trust, rendered)
    {

        CombinedClassNames.AutoSources = () => [
            Theme,
            Sidebar,
            Background,
            .. (PageClasses ?? []),
            .. GivenClassNames
        ];
    }


    public override async Task ListenUp(Type? typeHint = null)
    {
        await base.ListenUp(typeHint);

        // try to inject css directly instead of loading it remotely
        foreach (var style in RealRegistry)
        {
            var names = style.Key.Assembly.GetManifestResourceNames();
            var resFile = names.Where(f => f.Contains("forms"));
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var include in style.Value)
            {
                // Use the relative path logic we discussed
                var exists = File.Exists(Path.Combine(appPath, "wwwroot", include));
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


    private List<string> GivenClassNames { get; set; } = [];
    public ClassNameCollection CombinedClassNames { get; } = [];
    public ClassNameCollection ClassNames { get => CombinedClassNames; set => GivenClassNames = [.. value]; }
    private List<string>? PageClasses = [];
    public string? Theme;
    public string? Sidebar { get; private set; }
    public string? Background;

    // TODO: move this to mainloader classes along side SetTitle
    public void SetPageClasses(List<string> classes)
    {
        PageClasses = classes;
    }

    public void SetTheme(string? classes)
    {
        var newClass = "theme-" + (classes?.ToLowerInvariant() ?? string.Empty);
        Theme = newClass;
    }

    public void SetSidebar(string? classes)
    {
        Sidebar = classes;
    }

    public void SetBackground(string? classes)
    {
        var newClass = "background-" + (classes?.ToLowerInvariant() ?? string.Empty);
        Background = newClass;
    }

    private void SetBackground(AnimationMode? theme)
    {
        var newClass = "background-" + (theme?.ToString()?.ToLowerInvariant() ?? string.Empty);
        Background = newClass;
    }

}

public partial class JavascriptOutlet(ICompositeProvider Service, ITrustProvider Trust, RenderStateProvider rendered)
    : RenderOutlet(Service, Trust, rendered), IJavascriptOutlet
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

