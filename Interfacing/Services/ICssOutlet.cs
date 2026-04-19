
using Microsoft.Win32;
using System.Net.Http;

namespace Interfacing.Services;



public partial class ClassyService : IHasClass, IDisposable
{
    protected ICompositeProvider Service { get; }

#if false
    protected string BuildRenderTree()
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
#endif


    public void SetUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Trim('/'))) PageClasses = ["Home"];
        else PageClasses = [..uri.Split('?')[0]
            .Split('/')
            .Select(seg => seg.ToSafe())
        ];
    }


    public ClassyService(ICompositeProvider service, HttpClient? _client = null)
    {
        Service = service;
        Http = _client;
        _ = LoadSvg();

        CombinedClassNames.AutoSources = () => [
            Theme,
            Sidebar,
            Background,
            .. (PageClasses ?? []),
            .. GivenClassNames
        ];
    }

    public async Task LoadSvg()
    {
        if (SvgString != null) return;

        try
        {
            if (Http?.GetStringAsync("triangle.svg") is Task<string> task
                && await task is string icon)
            {
                SvgString ??= icon;
            }
        }
        catch
        { }

        if (SvgString != null) return;

        var root = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(root, "triangle.svg")))
        {
            SvgString ??= File.ReadAllText(Path.Combine(root, "triangle.svg"));
        }
        else if (File.Exists(Path.Combine(root, "wwwroot/triangle.svg")))
        {
            SvgString ??= File.ReadAllText(Path.Combine(root, "wwwroot/triangle.svg"));
        }
    }

    public static string? SvgString { get; private set; } = null;
    private readonly HttpClient? Http;

    public Action<object> LogoContent => __builder => (__builder as dynamic).AddMarkupContent(0, SvgString);


    protected static List<string> TypeToIncludes(Type type) => type switch
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
        //_ when type.Name.Contains("Layout", StringComparison.InvariantCultureIgnoreCase) => [
        //    "/_content/RazorSharp/css/main.css"],
        _ => []
    };

    public Type? RouteHint { get; private set; } = null;
    private Type? layout = null;
    private bool IsClosing = false;
    public List<string> Registry { get => [.. RealRegistry.SelectMany(list => list.Value).Distinct()]; }
    protected readonly ConcurrentDictionary<Type, List<string>> RealRegistry = [];
    private readonly Dictionary<string, string> _filePresence = [];

    public virtual async Task ListenUp(Type? typeHint = null, Type? _layout = null, IHasChildren? loader = null)
    {
        if (typeHint != null)
            RouteHint = typeHint;
        if (_layout != null)
            layout = _layout;


        if (IsClosing) return;
        if (typeHint == null)
        {
            await Task.Delay(800); // wait for layout to insert the component

            if (IsClosing) return;
        }

        List<Type?> components = [
            RouteHint,
            layout,
            //Rendered._container?.GetType(),
            //..Rendered._container?.GetChildComponents().Select(c => c.GetType()) ?? []
        ];

        var HasChanged = false;
        var Starting = Registry.ToList();

        foreach (var type in components.OfType<Type>())
        {
            if (type == null) continue;

            var includes = type.GetInterfaces()
                .Concat(type.BaseType != null ? [type, type.BaseType] : [type])
                .SelectMany(TypeToIncludes)
                .ToList();

            if (RealRegistry.TryGetValue(type, out var list))
            {
                foreach (var inc in includes)
                    if (!list.Contains(inc))
                    {
                        list.Add(inc);
                        HasChanged = true;
                    }
            }
            else
            {
                RealRegistry[type] = includes;
                HasChanged = true;
            }
        }


        foreach (var component in RealRegistry)
        {
            if (!components.Contains(component.Key))
            {
                //RealRegistry.TryRemove(component);
            }
            // don't trigger removals, just let them happen
        }

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


        var Ending = Registry.ToList();
        if (HasChanged && Ending.Except(Starting).Any()) // adds
        {
            Container ??= loader ?? Service.GetService<IHasChildren>();
            if (Container?.HasChanged() is Task task) await task;
            Console.WriteLine("Registry updated: " + JsonSerializer.Serialize(Registry));
        }

    }

    public void Dispose()
    {
        IsClosing = true;
        GC.SuppressFinalize(this);
    }

    private List<string> GivenClassNames { get; set; } = [];
    public ClassNameCollection CombinedClassNames { get; } = [];
    public ClassNameCollection ClassNames { get => CombinedClassNames; set => GivenClassNames = [.. value]; }
    private List<string>? PageClasses = [];
    public string? Theme;
    public string? Sidebar { get; private set; }
    public string? Background;
    private IHasChildren? Container;

    // TODO: move this to mainloader classes along side SetTitle
    public void SetPageClasses(List<string> classes, Type? typeHint = null)
    {
        PageClasses = classes;
        if (typeHint != null)
            RouteHint = typeHint;

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

