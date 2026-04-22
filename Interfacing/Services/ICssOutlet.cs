
using System.Net.Http;

namespace Interfacing.Services;



public partial class ClassyService : IHasClass, IDisposable
{


    public void SetUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Trim('/'))) PageClasses = ["Home"];
        else PageClasses = [..uri.Split('?')[0]
            .Split('/')
            .Select(seg => seg.ToSafe())
        ];
    }


    static ClassyService()
    {

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

        if (SvgString != null) return;

        _ = LoadSvg();

    }



    public static async Task LoadSvg()
    {
        if (SvgString != null) return;

        try
        {
            var Http = new HttpClient();

            if (Http?.GetStringAsync("triangle.svg") is Task<string> task
                && await task is string icon)
            {
                SvgString ??= icon;
            }
        }
        catch
        { }

    }

    public static string? SvgString { get; private set; } = null;

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
    private bool IsClosing = false;
    public List<string> Registry { get => [.. RealRegistry.SelectMany(list => list.Value).Distinct()]; }
    protected readonly List<Type> LoadedTypes = [];
    protected Dictionary<Type, List<string>>? StoredRegistry = null;
    protected Dictionary<Type, List<string>> RealRegistry
    {
        get
        {
            if (StoredRegistry != null) return StoredRegistry;
            StoredRegistry = [];
            foreach (var type in LoadedTypes)
            {
                if (type == null) continue;

                var includes = type.GetInterfaces()
                    .Concat(type.BaseType != null ? [type, type.BaseType] : [type])
                    .SelectMany(TypeToIncludes)
                    .ToList();

                if (StoredRegistry.TryGetValue(type, out var list))
                {
                    foreach (var inc in includes)
                        if (!list.Contains(inc))
                        {
                            list.Add(inc);
                        }
                }
                else
                {
                    StoredRegistry[type] = includes;
                }
            }
            return StoredRegistry;
        }
    }

    private readonly Dictionary<string, string> _filePresence = [];

    public void SetRoute(Type? typeHint = null)
    {
        if (typeHint != null && !LoadedTypes.Contains(typeHint))
        {
            LoadedTypes.Add(typeHint);
            StoredRegistry = null;
            Console.WriteLine("Registry updated: " + JsonSerializer.Serialize(Registry));
            _ = TryLoadingStylesText();
        }

    }


    protected async Task TryLoadingStylesText()
    {


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


    public void Dispose()
    {
        IsClosing = true;
        GC.SuppressFinalize(this);
    }

    private List<string> GivenClassNames { get; set; } = [];
    ClassNameCollection? StoredClassNames = null;
    public ClassNameCollection CombinedClassNames
    {
        get
            => StoredClassNames ??= new ClassNameCollection
            {
                AutoSources = () => [
                    Theme,
                    Sidebar,
                    Background,
                    .. (PageClasses ?? []),
                    .. GivenClassNames
                ]
            };
    }
    public ClassNameCollection ClassNames { get => CombinedClassNames; set => GivenClassNames = [.. value]; }
    private List<string>? PageClasses = [];
    public string? Theme;
    public string? Sidebar { get; private set; }
    public string? Background;


    // TODO: move this to main loader classes along side SetTitle
    public void SetClasses(List<string>? classes)
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

