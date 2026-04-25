
namespace Interfacing.Services;

public interface IHasBuilder
{
    static abstract void BuildServices(IServiceCollection services, string? key = null);
}

public interface IHasService
{
    IServiceProvider Services { get; }
}

public interface ICompositeProvider : IServiceProvider, IServiceProviderIsService, IHasService, IDisposable
{
    //List<IServiceProvider> PluginContainers { get; }
}

// for the very staticy stuff
public interface IHasCurrent<T>
{
    static abstract T? Current { get; }
}

public interface IHasPlugins
{
    // this is the name of the setting indicating its installed or null for not
    //   this is used to shortcut the enabled process after the first load
    // should return a Task<string?>
    static abstract Delegate Installed { get; }
    // this is what the service manager should automatically add to DI if enabled after being checked for installed 
    // should return a Dictionary<Type, (string Name, string Icon)>?
    static abstract Delegate Plugins { get; }
}

public interface IHasFeatures
{
    // this is a list of fully qualified type names of enabled features for short reference
    // should return a Task<List<string>?>
    static abstract Delegate Installed { get; }
    // this is what the UX should display after checking installed
    // should return a Dictionary<Type, (string Name, string Icon)>?
    static abstract Delegate DisplayNames { get; }
}

public interface IHasSettings
{
    static abstract Delegate ShowSettings { get; }
    static abstract Delegate SettingInsert { get; }
}

public interface IHasStatus
{
    static abstract Delegate ShowStatus { get; }
    static abstract Delegate StatusInsert { get; }
}

public interface IHasHome
{
    static abstract Delegate ShowHome { get; }
    static abstract Delegate HomeInsert { get; }
}

public record PluginContract(
    string Title,
    string InstallPath,
    bool IsTrusted,
    AssemblyInfo Metadata);

public class Dumbass { }

public interface ITrustProvider
{
    event Action<PluginContract> OnAssemblyLoaded;
    event Action? OnSettled;
    event Func<Task>? OnSettledAsync;
    void Enable(string ass);
    void Disable(string ass);
    TrustedState State { get; }
}

// TODO: putting all this in a separate class because i want to make a single copy in between
//   threads that might affect lists, so it only has to manage itself instead of be preventative 
//   for all the implementors
public partial class TrustedState(ITrustProvider? _trust) : IDisposable
{
    // TODO: make json compatible by using the based types needed for output
    [JsonIgnore]
    public ITrustProvider? Trust { get; set; } = _trust;
    [JsonIgnore]
    public List<Assembly> Seen { get; set; } = [];
    [JsonIgnore]
    public List<Type> Layouts { get; set; } = [];
    [JsonIgnore]
    public List<Assembly> Routable { get; set; } = [];
    [JsonIgnore]
    public List<Type> CatchAll { get; set; } = [];
    [JsonIgnore]
    public List<Type> Roots { get; set; } = [];
    [JsonIgnore]
    public List<Type> AllRoutes { get; set; } = [];
    [JsonPropertyName(nameof(DisplayLayouts))]
    public Dictionary<string, string> DisplayLayouts { get => Layouts.ToDictionary(t => t.AssemblyQualifiedName ?? t.FullName ?? t.Name, t => t.Name); }
    [JsonPropertyName(nameof(DisplayRoutable))]
    public List<string> DisplayRoutable { get => [.. Routable.Select(ass => ass.ToName())]; }
    [JsonPropertyName(nameof(DisplayCatchAll))]
    public Dictionary<string, string> DisplayCatchAll { get => CatchAll.ToDictionary(t => t.AssemblyQualifiedName ?? t.FullName ?? t.Name, t => t.Name); }
    [JsonPropertyName(nameof(DisplayRoots))]
    public Dictionary<string, string> DisplayRoots { get => Roots.ToDictionary(t => t.AssemblyQualifiedName ?? t.FullName ?? t.Name, t => t.Name); }
    [JsonPropertyName(nameof(DisplayAllRoutes))]
    public Dictionary<string, string> DisplayAllRoutes { get => AllRoutes.ToDictionary(t => t.AssemblyQualifiedName ?? t.FullName ?? t.Name, t => t.Name); }
    [JsonPropertyName(nameof(IsBootstrapping))]
    public bool IsBootstrapping { get; set; } = true;
    [JsonPropertyName(nameof(Error))]
    public string Error { get; set; } = string.Empty;
    [JsonPropertyName(nameof(PluginFiles))]
    public string[] PluginFiles { get; set; } = [];
    [JsonIgnore]
    public Action<PluginContract?>? NotifyDelegate = null;
    [JsonPropertyName(nameof(EnabledAssemblies))]
    public Dictionary<string, bool> EnabledAssemblies { get; } = [];
    [JsonPropertyName(nameof(SystemEnabledAssemblies))]
    public Dictionary<string, bool> SystemEnabledAssemblies { get; } = [];

    [JsonPropertyName(nameof(EnabledAssMappings))]
    public List<Assembly>? EnabledAssMappings { get; set; } = null;
    [JsonPropertyName(nameof(DependedAssemblies))]
    public Dictionary<string, List<string>>? DependedAssemblies { get; set; }
    [JsonIgnore]
    public List<Assembly>? DependedAssMappings { get; set; } = null;
    [JsonIgnore]
    public Dictionary<string, Assembly> LoadedAssemblies = [];
    [JsonPropertyName(nameof(RequiredAssMappings))]
    public List<Assembly>? RequiredAssMappings { get; set; } = [];
    [JsonPropertyName(nameof(DiscoveredStatus))]
    public Dictionary<string, PluginContract> DiscoveredStatus { get; set; } = [];
    [JsonIgnore]
    public Dictionary<Type, List<Type>> StoredServiceable { get; set; } = [];
    [JsonPropertyName(nameof(Scanning))]
    public string Scanning { get; set; } = string.Empty;
    [JsonIgnore]
    public List<Type> AllPlugins { get; } = [];
    [JsonPropertyName(nameof(IsRebuilding))]
    public bool IsRebuilding { get; set; } = false;
    [JsonPropertyName(nameof(StoredRoot))]
    public string? StoredRoot = null;
    [JsonIgnore]
    public Type? SetRoot
    {
        get => StoredRoot != null ? Type.GetType(StoredRoot) : null;
        set => StoredRoot = value?.AssemblyQualifiedName;
    }
    public void Dispose()
    {
        Trust?.OnAssemblyLoaded -= this.NotifyDelegate;
        GC.SuppressFinalize(this);
    }
    public void Deconstruct(out bool? isBootstrapping, out string? error, out string? scanning)
    {
        isBootstrapping = IsBootstrapping;
        error = Error;
        scanning = Scanning;
    }

    public void Deconstruct(out Dictionary<string, string>? layouts,
        out List<string>? routable,
        out Dictionary<string, string>? catchAll,
        out Dictionary<string, string>? roots,
        out Dictionary<string, string>? allRoutes)
    {
        layouts = DisplayLayouts;
        routable = DisplayRoutable;
        catchAll = DisplayCatchAll;
        roots = DisplayRoots;
        allRoutes = DisplayAllRoutes;
    }
}



public interface ITrustStatic : ITrustProvider // dumbass DI compile error
{
    static abstract List<Type> Layouts { get; }
    static abstract List<Assembly> Seen { get; }
    static abstract List<Assembly> Routable { get; }
    static abstract List<Type> CatchAll { get; }
    static abstract List<Type> Roots { get; }
    static abstract List<Type> AllRoutes { get; }

}

public static class TrustedExtensions
{
    // type shit
    public static Type? DefaultRoot(this ITrustStatic Trust)
    {
        var defaultRoot = typeof(TrustedExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == nameof(DefaultRoot) && m.IsGenericMethod)
            ?? throw new InvalidOperationException("Cannot render DefaultRoot method.");
        var defaultConcrete = defaultRoot.MakeGenericMethod(Trust.GetType());
        return defaultConcrete.Invoke(null, [Trust]) as Type;
    }

    public static Type? DefaultRoot(this TrustedState Trust)
    {
        if (Trust.SetRoot != null) return Trust.SetRoot;
        if (Trust.Roots.Count > 0 
            && !Trust.Roots.First().Name.Contains("default", StringComparison.InvariantCultureIgnoreCase)) 
            return Trust.Roots.First();
        if (Trust.CatchAll.Count > 0) return Trust.CatchAll.First();
        lock (Trust.AllRoutes)
            if (Trust.AllRoutes
            .OrderBy(r => r.Name.Contains("plugins", StringComparison.InvariantCultureIgnoreCase)
                || !r.Name.Contains("default", StringComparison.InvariantCultureIgnoreCase)
                ? -1 : 0)
            .FirstOrDefault() is Type any)
                return any;
        return null;
    }
}



public record AssemblyInfo(string? Product, string? Company, string? Publisher, string? Package, LevelOfTrust TrustLevel = LevelOfTrust.Untrusted);

public enum LevelOfTrust : int
{
    Untrusted = 0,
    Meta = 1, // title from a known publisher
    Required = 2,
    Published = 3, // strongly named
    Signed = 4, // strong name or authenticode signature from a trusted authority
    Mine = 5, // title from a known publisher and matches an assembly already loaded into the app domain
    Verified = 6, // verified through windows signing
    Trusted = 7, // cert is already in user store
    Full = 8 // manually marked as trusted by user
}

public interface IHasForms
{

}

public interface IHasCover
{

}


public interface IAsyncRender
{
    Task<Delegate?> Render(
        ICompositeProvider? Composite
    );
}

public interface IHasRender
{
    Delegate Render(
        ICompositeProvider? Composite
    );
}


public interface IHasStaticRender
{
    static abstract Delegate RenderStatic(
        ICompositeProvider? Composite
    );
}



public interface IHasRender<T> where T : class
{
    static abstract Task<Delegate?> RenderMain(
        ICompositeProvider? Composite,
        T? Myself
    );
}

internal static class TrustedInterfaceExtensions
{

    public static string ToName(this Assembly ass)
    {
        var file = Path.GetFileNameWithoutExtension(ass.Location);
        return string.IsNullOrWhiteSpace(file) ?
                    ass.FullName?.Split(',')[0]
                    ?? ass.GetName().Name
                    ?? ass.GetName().FullName.Split(',')[0]
                    : file;
    }

    public static string ToName(this AssemblyName ass)
    {
        return ass.Name ?? ass.FullName.Split(',')[0];
    }

}