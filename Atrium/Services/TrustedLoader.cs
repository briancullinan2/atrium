
#if !BROWSER
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Storage;
using Atrium.Components;
#endif


#if WINDOWS
using System.Runtime.InteropServices;
using Atrium.Platforms.Windows;
#endif

namespace Atrium.Services;

public partial class TrustedLoader : ITrustProvider, IHasCurrent<AppDomain>, IDisposable
{
    public static AppDomain Current { get => AppDomain.CurrentDomain; }
    private static event Action? InternalOnSettled;
    public event Action? OnSettled
    {
        add
        {
            if (value == null) return;
            // incase anything forgets this bool setOnce = false pattern
            if (InternalOnSettled?.GetInvocationList().Contains(value) == true) return;
            InternalOnSettled += value;
            if (IsRebuilding || IsBootstrapping) return; // allow subscribers in without immediately retriggering
            _ = SettleServices(null);
        }
        remove
        {
            InternalOnSettled -= value;
        }
    }

    // TODO: use a ConcurrentDictionary and actually await the calls?
    // fires at least once for everybody even if services aren't rebuilt
    private static event Func<Task>? InternalOnSettledAsync;
    public event Func<Task>? OnSettledAsync
    {
        add
        {
            if (value == null) return;
            // incase anything forgets this bool setOnce = false pattern
            if (InternalOnSettledAsync?.GetInvocationList().Contains(value) == true) return;
            InternalOnSettledAsync += value;
            if (IsRebuilding || IsBootstrapping) return; // allow subscribers in without immediately retriggering
            _ = SettleServices(null);
        }
        remove
        {
            InternalOnSettledAsync -= value;
        }
    }


    private static readonly Func<string?, bool> FILTER_MICROSOFT_DLLS_BY_NAME;
    static TrustedLoader()
    {
        FILTER_MICROSOFT_DLLS_BY_NAME = title => string.IsNullOrEmpty(title) || title.StartsWith("System.") || title.StartsWith("Microsoft.") || title.StartsWith("WinRT.");

        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;

        ReloadAppDomain();
    }


    public void Enable(string ass)
    {
        EnabledAssemblies.Add(ass, true);
        Enable(ass, false);
    }

    public static void Enable(string ass, bool fromLoader)
    {
        CachedEnabledAssMappings = null;
        CachedDependedAssemblies = null;
        CachedDependedAssMappings = null;
#if !BROWSER
        Preferences.Default.Set("PluginEnabled" + ass, true);
#endif
        // TODO: if we end up here in web assembly, need to check ISettings instead 

        if (!fromLoader)
        {
            // prevent recursion
            try
            {
                var loaded = AppDomain.CurrentDomain.Load(new AssemblyName(ass));
                if (loaded == null) return;
                // temporary whatever
                StoredAssemblies.TryAdd(ass, loaded);
                TryFindingInterestingTypes(loaded);
            }
            catch (Exception ex)
            {
                // do something statusy
                Error = ex.Message;
            }
        }
    }

    public void Disable(string ass)
    {
        EnabledAssemblies.Remove(ass);
        CachedEnabledAssMappings = null;
        CachedDependedAssemblies = null;
        CachedDependedAssMappings = null;
#if !BROWSER
        Preferences.Default.Set("PluginEnabled" + ass, false);
#endif
    }

    private static readonly ConcurrentDictionary<string, string> Tried = [];

#if false


    internal static Dictionary<Type, Type?> DefaultTypes { get; } = new () {
        { typeof(TrustedLoader), null },
        { typeof(ITrustProvider), typeof(TrustedLoader) },
        { typeof(PluginActivator), null },
        { typeof(LogoService), null },
        { typeof(CssOutlet), null },
        { typeof(JavascriptOutlet), null },
        { typeof(WindowManager), null },
        { typeof(IWindowManager), typeof(WindowManager) },
        { typeof(CompositeServiceProvider), null },
        { typeof(IServiceProvider), typeof(ICompositeProvider) },
        { typeof(ICompositeProvider), typeof(CompositeServiceProvider) },
        { typeof(IServiceScopeFactory), typeof(CompositeServiceProvider) },
        { typeof(IComponentActivator), typeof(PluginActivator) },
        { typeof(IServiceProviderIsService), typeof(PluginActivator) },


    };

    internal static void BuildBaseServices(IServiceCollection collection, IServiceProvider existing)
    {

        //var hasAuth = AllServices.Any(t => t.Extends(typeof(IAuthService)));

        //if (hasAuth)
        //{
        //    collection.AddAuthorizationCore();
        //   collection.AddCascadingAuthenticationState();
        //}

        collection.BuildServices(DefaultTypes.Keys.ToList());
    }

#endif


    public void BuildServices(IServiceCollection collection, List<Type>? types)
    {
        if (types == null)
        {
            var mappings = EnabledAssMappings
                .Concat(DependedAssMappings)
                .Concat(RequiredAssMappings)
                .Where(MetadataReaderExtensions.IsMine)
                .ToList();
            var currents = mappings.SelectMany(BuilderExtensions.GetAssTypesSafely).GetServiceable().ToList();
            collection.BuildServices(currents);
        }
        else
            collection.BuildServices(types);
    }



    private static async Task SettleServices(Assembly? newAss)
    {
        try
        {
            await Task.Delay(300);

            if (IsRebuilding) return;

            IsRebuilding = true; // was trying to decide to put it here or 3 lines down



            CachedDependedAssemblies = null;
            CachedEnabledAssMappings = null;
            CachedDependedAssMappings = null;

            var mappings = newAss?.GetReferencedAssemblies()
                .Select(ass => ass.ToName())
                .Where(ass => !FILTER_MICROSOFT_DLLS_BY_NAME(ass))
                .ToList();


            var collection = new ServiceCollection();

            // TODO: check depended assemblies is empty compared to loaded assemblies then offer an OnSettled even if its preloading is done
            var missing = mappings?.Where(ass => Tried.ContainsKey(ass) != true).ToList();
            if (missing?.Count > 0)
            {
                var parallel = Environment.ProcessorCount - 4;

                var options = new ParallelOptions
                {
                    // Leave at least one or two cores for the UI thread
                    MaxDegreeOfParallelism = Math.Max(1, parallel)
                };

                foreach (var ass in missing ?? []) // prevent recursion
                    Tried.TryAdd(ass, ass);

                _ = Parallel.ForEachAsync(missing ?? [], options, async (ass, ct) =>
                {
                    try
                    {
                        AppDomain.CurrentDomain.Load(ass);
                    }
                    catch (Exception)
                    {

                    }
                }).ContinueWith(t => SettleServices(null)); // make sure it fires at least once more after we quit below

                IsRebuilding = false;

                return; // might as well duck out now because we know more are coming
            }

            lock (CachedPluginContracts)
            {
                var old = InternalOnSettled;
                var oldAsync = InternalOnSettledAsync;
                InternalOnSettled = null; // make the fuckers resubscribe anyways, hit only once
                InternalOnSettledAsync = null;
                old?.Invoke();
                _ = oldAsync?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {

            IsRebuilding = false;

        }
    }


    public Dictionary<string, bool> EnabledAssemblies { get; } = [];

    static List<Assembly>? CachedEnabledAssMappings { get; set; } = null;
    internal List<Assembly> EnabledAssMappings
    {
        get => CachedEnabledAssMappings
            ??= [..EnabledAssemblies.Where(kvp => kvp.Value).Select(kvp => kvp.Key)
        .Select(ass => LoadedAssemblies.TryGetValue(ass, out var loaded) ? loaded : null)
        .OfType<Assembly>()];
    }

    // TODO: reset to null on user input
    static Dictionary<string, List<string>>? CachedDependedAssemblies { get; set; } = null;
    public Dictionary<string, List<string>> DependedAssemblies
    {
        get => CachedDependedAssemblies
            ??= EnabledAssMappings
        .Concat(RequiredAssMappings)

        .SelectMany(parentAss =>
            parentAss.GetReferencedAssemblies()
                .Select(refAss => new
                {
                    Parent = parentAss.ToName(),
                    Dependency = refAss.ToName()
                }))
        .Concat(RequiredAssMappings.Select(a => new
        {
            Parent = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).ToName(),
            Dependency = a.ToName()
        }))
        // Filter out null names if any
        .Where(x => x.Parent != null && x.Dependency != null)
        // Group by the Dependency (The "Required" assembly on the left)
        .GroupBy(x => x.Dependency)
        .ToDictionary(
            g => g.Key, // The Required Assembly (The "Source")
            g => g.Select(x => x.Parent).Distinct().ToList() // The "Requirees" (The "Dependents")
        );
    }

    static List<Assembly>? CachedDependedAssMappings { get; set; } = null;
    public List<Assembly> DependedAssMappings
    {
        get => CachedDependedAssMappings
            ??= [..RequiredAssMappings, ..EnabledAssMappings
        .Concat(RequiredAssMappings)
        .SelectMany(parentAss => parentAss.GetReferencedAssemblies())
        .Select(ass => {
            if (LoadedAssemblies.TryGetValue(ass.ToName(), out var loaded) == true) return loaded;
            return null;
        })
        .OfType<Assembly>()
        .Distinct()
        ];
    }


    public static List<string> RequiredAssemblies { get; } = [..new List<AssemblyName?>
        { Assembly.GetEntryAssembly()?.GetName(),
          Assembly.GetExecutingAssembly().GetName(),
        }.Concat((Assembly.GetEntryAssembly()??Assembly.GetExecutingAssembly()).GetReferencedAssemblies() ?? [])
        .OfType<AssemblyName>()
        .Select(MetadataReaderExtensions.ToName)
        ];

    private List<Assembly>? CachedRequiredAssMappings { get; set; } = null;
    public List<Assembly> RequiredAssMappings
    {
        get => CachedRequiredAssMappings
            ??= [..RequiredAssemblies
        .Select(ass => {
            if (LoadedAssemblies.TryGetValue(ass, out var loaded) == true) return loaded;
            return null;
        })
        .OfType<Assembly>()
        .Distinct()
        ];
    }

    private static readonly ConcurrentDictionary<string, Assembly> StoredAssemblies = [];


    public Dictionary<string, Assembly> LoadedAssemblies { get => StoredAssemblies.ToDictionary(); }


    [RequiresAssemblyFiles]
    public TrustedLoader(
        IComponentActivator? plugin = null,
        ICompositeProvider? provider = null)
    {
        Plugin = plugin;
        Provider = provider;

        var parallel = Environment.ProcessorCount - 4;

        var options = new ParallelOptions
        {
            // Leave at least one or two cores for the UI thread
            MaxDegreeOfParallelism = Math.Max(1, parallel)
        };

        IsBootstrapping = true;

        _ = CheckPluginFiles();

        _ = Parallel.ForEachAsync(LoadedAssemblies.Values, options, async (ass, ct) =>
        {
            // this is why i put the Seen gate on this and the file scan
            string title = ass.ToName();
            if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;
            var contract = new PluginContract(
                Title: title,
                InstallPath: ass.Location, // Will be empty string in Single-File
                IsTrusted: true,
                Metadata: ass.GetAssemblyInfo()
            );
            DiscoveredStatus.TryAdd(title, contract);
            OnAssemblyLoaded?.Invoke(contract);
        }).ContinueWith((_) => SettleServices(null), TaskContinuationOptions.NotOnFaulted);

        PrivateAssemblyLoaded += TrustedLoader_PrivateAssemblyLoaded;
        /*
        // TODO: make contexts unloadable LoadPlugins()
        var context = new AssemblyLoadContext("PluginContext", isCollectible: true);
        var assembly = context.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "Interfacing.dll"));
        */

    }

    private void TrustedLoader_PrivateAssemblyLoaded(PluginContract obj)
    {
        OnAssemblyLoaded?.Invoke(obj);
    }

    public static bool IsBootstrapping { get; set; } = false;
    static ConcurrentDictionary<string, PluginContract> CachedPluginContracts { get; } = new();
    public Dictionary<string, PluginContract> DiscoveredStatus { get => CachedPluginContracts.ToDictionary(); }
    public static Dictionary<Type,List<Type>> Serviceable { get; } = [];
    public static List<Type> AllPlugins { get; } = [];

    [RequiresAssemblyFiles("Uses Location for plugin tracking")]
    private static void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        var assembly = args.LoadedAssembly;

        // Fallback: If Location is empty (Single File), use the Simple Name
        string location = assembly.Location;
        string title = assembly.ToName();

        Console.WriteLine("Assembly loaded: " + title + " : " + location);
        StoredAssemblies.TryAdd(title, assembly);

#if !BROWSER
        if (Preferences.Default.Get("PluginEnabled" + title, false))
            Enable(title, true);
        else
#endif
            if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;

        if (Seen.Contains(assembly)) return;

        Task.Run(async () =>
        {
            TryFindingInterestingTypes(assembly);

            var contract = new PluginContract(
                Title: title,
                InstallPath: location, // Will be empty string in Single-File
                IsTrusted: true,
                Metadata: assembly.GetAssemblyInfo()
            );
            CachedPluginContracts.TryAdd(title, contract);
            PrivateAssemblyLoaded?.Invoke(contract);
            await SettleServices(assembly);
        });

    }


    static public readonly List<Type> Layouts = [];
    static readonly List<Assembly> Seen = [];
    static public readonly List<Assembly> Routable = [];
    static public readonly List<Type> CatchAll = [];
    static public readonly List<Type> Roots = [];
    static public readonly List<Type> AllRoutes = [];

    private static void TryFindingInterestingTypes(Assembly ass)
    {
        lock (Seen)
        {
            if (Seen.Contains(ass))
                return;

            Seen.Add(ass);
        }

        if(ass.IsMine())
        {
            Console.WriteLine(ass.ToName());
        }

        var allTypes = ass.GetAssTypesSafely();

        var routable = false;

        foreach (var type in allTypes)
        {
            try
            {
                if (typeof(LayoutComponentBase).IsAssignableFrom(type)
                    && type != typeof(LayoutComponentBase)
                    && !Layouts.Contains(type))
                    Layouts.Add(type);

                if (typeof(IHasPlugins).IsAssignableFrom(type)
                    && !AllPlugins.Contains(type))
                    AllPlugins.Add(type);

                if (type.IsServiceable() && !Serviceable.ContainsKey(type))
                    lock(Serviceable)
                        Serviceable.TryAdd(type, [..type.GetInterfaces()]);

                if (type.GetCustomAttributes<RouteAttribute>().FirstOrDefault() is RouteAttribute attr
                    && type != typeof(Atrium.Components.PluginsPage)
                    && !AllRoutes.Contains(type)) // we already know about ourselves
                {
                    routable = true;
                    if (attr.Template.StartsWith("/*")
                        || attr.Template.StartsWith("/{*")
                        || attr.Template.StartsWith('*'))
                        CatchAll.Add(type);

                    if (attr.Template == "/")
                        Roots.Add(type);

                    AllRoutes.Add(type);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        if (routable && !Routable.Contains(ass))
            Routable.Add(ass);
    }



    public void Dispose()
    {
        PrivateAssemblyLoaded -= TrustedLoader_PrivateAssemblyLoaded;
        GC.SuppressFinalize(this);
    }



    protected static async Task CheckPluginFiles()
    {
        // TODO: refresh button
        PluginFiles ??= Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        var counter = PluginFiles.Length;
        var parallel = Environment.ProcessorCount - 4;

        var options = new ParallelOptions
        {
            // Leave at least one or two cores for the UI thread
            MaxDegreeOfParallelism = Math.Max(1, parallel)
        };

        // Process files in parallel batches to speed up metadata extraction
        await Parallel.ForEachAsync(PluginFiles, options, async (file, ct) =>
        {
            --counter;

            if (counter <= 1) // notify UX
                IsBootstrapping = false;

            var title = Path.GetFileNameWithoutExtension(file);

#if !BROWSER
            if (Preferences.Default.Get("PluginEnabled" + title, false))
            {
                Enable(title, false);
                if(StoredAssemblies.TryGetValue(title, out var ass))
                    TryFindingInterestingTypes(ass);
            }
            else 
#endif
            if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;

            var unloadedContract = new PluginContract(
                Title: title,
                InstallPath: file,
                IsTrusted: false,
                Metadata: new AssemblyInfo("Not Loaded", "", "", Path.GetFileNameWithoutExtension(file), LevelOfTrust.Untrusted)
            );
            CachedPluginContracts.TryAdd(title, unloadedContract);
            PrivateAssemblyLoaded?.Invoke(unloadedContract);

            await Task.Delay((counter % parallel) * 100, ct); // burst mode

            var trust = await GetTrustedAsync(file);


            if (trust == null || (int)trust < (int)LevelOfTrust.Meta)
                return;

            var contract = new PluginContract(
                Title: title,
                InstallPath: file,
                IsTrusted: (int)trust.Value > 2,
                Metadata: new AssemblyInfo("Not Loaded", "", "", Path.GetFileNameWithoutExtension(file), trust.Value)
            );

            // Thread-safe update to the UI list
            if (CachedPluginContracts.ContainsKey(title))
                CachedPluginContracts[title] = contract;
            else
                CachedPluginContracts.TryAdd(title, contract);

            // Tell the UI to refresh as each item arrives
            PrivateAssemblyLoaded?.Invoke(contract);

            if ((int)trust >= (int)LevelOfTrust.Published)
            {
                var meta = await GetAssemblyInfoAsync(file);
                if (meta != null)
                {
                    var newContract = new PluginContract(
                        Title: title,
                        InstallPath: file,
                        IsTrusted: (int)trust.Value > 2,
                        Metadata: meta
                    );
                    if (CachedPluginContracts.ContainsKey(title))
                        CachedPluginContracts[title] = newContract;
                    else
                        CachedPluginContracts.TryAdd(title, newContract);
                    PrivateAssemblyLoaded?.Invoke(newContract);
                }
            }
        });

        IsBootstrapping = false;
        await SettleServices(null);
    }



    public static List<Type> EnabledPlugins { get; private set; } = [];

    [RequiresAssemblyFiles()]
    public async Task CheckStatus()
    {
        if (Provider == null) return;
        EnabledPlugins ??= await GetEnabledPlugins(Provider);
        foreach (var plugin in EnabledPlugins)
        {
            var title = plugin.Assembly.ToName();
            var newContract = new PluginContract(
                    Title: plugin.AssemblyQualifiedName ?? plugin.FullName ?? plugin.Name,
                    InstallPath: plugin.Assembly.Location,
                    IsTrusted: false, //metadata?.IsTrusted ?? false,
                    Metadata: plugin.GetAssemblyInfo()
                );
            DiscoveredStatus.TryAdd(title, newContract);
            OnAssemblyLoaded?.Invoke(newContract);
        }
    }

    // TODO: use this on service startup? way to bootstrap another container?
    public static async Task<List<Type>> GetEnabledPlugins(ICompositeProvider service)
    {
        List<Type> enabledPlugins = [];
        foreach (var plugin in AllPlugins)
        {
            var myDelegate = plugin.GetProperty(nameof(IHasPlugins.Installed), BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as Delegate;
            if (myDelegate == null || typeof(Task<string?>).IsAssignableFrom(Nullable.GetUnderlyingType(myDelegate.Method.ReturnType)
                ?? myDelegate?.Method.ReturnType) != true)
                throw new InvalidOperationException("IHasPlugins.Installed delegate must return a Task<string?> with the name of the setting it used to check if its installed or not" + myDelegate?.Method);
            var result = myDelegate.InvokeService(service);
            if (result is Task task)
            {
                await task;
#pragma warning disable IDE0260 // VS fucks up on dynamics
                if ((result as dynamic)?.Result == true)
                    enabledPlugins.Add(plugin);
#pragma warning restore IDE0260 // VS fucks up on dynamics
            }
        }
        return enabledPlugins;
    }


    public static bool IsLoading { get; private set; } = false;



    private static void ReloadAppDomain()
    {

        var asses = AppDomain.CurrentDomain.GetAssemblies();
        var assNames = asses.Select(MetadataReaderExtensions.ToName).ToList();
        var collisions = assNames.GroupBy(n => n).Where(g => g.Count() > 1).ToList();

        Console.WriteLine("Domain: " + JsonSerializer.Serialize(collisions) + " - " + JsonSerializer.Serialize(assNames));

        foreach (var ass in asses)
        {
            try
            {

                var title = ass.ToName();
                Console.WriteLine("Assembly loaded: " + title + " : " + ass.Location);
                StoredAssemblies.TryAdd(title, ass);
                if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) continue;
                TryFindingInterestingTypes(ass);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }

    private static string[]? PluginFiles { get; set; } = null;
    public static string? Error { get; private set; }

    // GUID for the Action to verify a file using the Authenticode Policy Provider
#if WINDOWS
    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
#endif
    //private static readonly string MyThumbprint = "024eb7945944bb29c8fc16b7e83e885cda191fdf";
    //private static readonly X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromStore(MyThumbprint);
    //private static string HomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    //private static string MyCertificatePath => Path.Combine(HomeDir, ".credentials\\my-code-signing.pfx");
    //private static X509Certificate2 Mine => X509CertificateLoader.LoadCertificateFromFile(MyCertificatePath);
    private static readonly List<string> Whitelist = ["B1FB6C91198947FC"];
    private readonly IComponentActivator? Plugin;
    private readonly ICompositeProvider? Provider;
    private static bool IsRebuilding = false;

    static event Action<PluginContract>? PrivateAssemblyLoaded;

    public event Action<PluginContract>? OnAssemblyLoaded;

    /*
    public static bool VerifyCertificate(string filePath, string? thumbprint = null)
    {
        try
        {
            // TODO: make sure chain is trusted
        }
        catch
        {
            // Likely a native DLL or corrupt file
            return false;
        }
    }
    */


    static async Task<LevelOfTrust?> GetTrustedAsync(string filePath, string? expectedPublicKeyToken = null)
    {
        LevelOfTrust? level = LevelOfTrust.Untrusted;
        try
        {
            if (RequiredAssemblies.Contains(Path.GetFileNameWithoutExtension(filePath)))
                level = LevelOfTrust.Required;

            var name = AssemblyName.GetAssemblyName(filePath);
            if (name != null)
                level = LevelOfTrust.Meta;
            else
                return null;

            var token = name?.GetPublicKeyToken();
            if (token != null)
                level = LevelOfTrust.Published;

            // TODO: fix flow for this

            if (Whitelist.Contains(Convert.ToHexString(name?.GetPublicKeyToken()!)))
                level = LevelOfTrust.Mine;

#if WINDOWS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            if (VerifyWindowsSignature(filePath, expectedPublicKeyToken))
                level = LevelOfTrust.Verified;
#endif
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return null;
        }

        //if (VerifyCertificate(filePath, expectedPublicKeyToken))
        //    level = LevelOfTrust.Signed;

        return level;
    }

    static async Task<AssemblyInfo?> GetAssemblyInfoAsync(string filePath, string? expectedPublicKeyToken = null)
    {
        var level = await GetTrustedAsync(filePath);
        var title = Path.GetFileNameWithoutExtension(filePath);

        if (level == null)
            return new AssemblyInfo(
                "Not Trustable",
                null,
                null,
                title,
                LevelOfTrust.Untrusted
            );

        if (level < LevelOfTrust.Published)
            return new AssemblyInfo(
                "Not Loaded",
                null,
                null,
                title,
                level.Value
            );

        // TODO: temporary
        AssemblyInfo? meta = null;
        if (StoredAssemblies.TryGetValue(title, out var ass))
            meta = new AssemblyInfo(
                ass.GetProduct(),
                ass.GetCompany(),
                ass.GetPublisher(),
                ass.GetPackage(),
                level.Value
            );
        //else if (EnabledAssemblies.TryGetValue(title, out var status) && status)
        //    meta = MetadataReaderExtensions.GetAssemblyInfo(filePath);


        if (meta == null) return new AssemblyInfo(
            "No Metadata",
            "",
            "",
            title,
            level.Value
        );


        return new AssemblyInfo(
            meta.Product,
            meta.Company,
            meta.Publisher,
            meta.Package,
            level.Value
        );
    }


#if WINDOWS
    public static bool VerifyWindowsSignature(string filePath, string? expectedPublicKeyToken = null)
    {
        var fileInfo = new WinTrust.WinTrustFileInfo(filePath);
        IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
        Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

        var trustData = new WinTrust.WinTrustData(fileInfoPtr);
        IntPtr trustDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(trustData));
        Marshal.StructureToPtr(trustData, trustDataPtr, false);

        expectedPublicKeyToken ??= WINTRUST_ACTION_GENERIC_VERIFY_V2.ToString();

        try
        {
            uint result = WinTrust.WinVerifyTrust(IntPtr.Zero, new Guid(expectedPublicKeyToken), trustDataPtr);
            return result == 0; // 0 = ERROR_SUCCESS
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
            Marshal.FreeHGlobal(trustDataPtr);
        }
    }
#endif

}
