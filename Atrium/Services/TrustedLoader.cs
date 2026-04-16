using Atrium.Components;
using Atrium.Extensions;
using Interfacing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Maui.Storage;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;




#if WINDOWS
using System.Runtime.InteropServices;
using Atrium.Platforms.Windows;
#endif

namespace Atrium.Services;

public partial class TrustedLoader : ITrustProvider, IHasCurrent<AppDomain>, IDisposable, IHasService
{
    public static AppDomain Current { get => AppDomain.CurrentDomain; }
    bool IsRebuilding = false;
    private event Action? InternalOnSettled;
    public event Action? OnSettled
    {
        add
        {
            InternalOnSettled += value;
            if (IsRebuilding) return;
            _ = RebuildServiceContainer();
        }
        remove
        {
            InternalOnSettled -= value;
        }
    }

    // TODO: use a ConcurrentDictionary and actually await the calls?
    private event Func<Task>? InternalOnSettledAsync;
    public event Func<Task>? OnSettledAsync
    {
        add
        {
            InternalOnSettledAsync += value;
            if (IsRebuilding) return;
            _ = RebuildServiceContainer();
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
    }



    private IServiceProvider? StoredServices = null;
    public IServiceProvider Services
    {
        get => StoredServices ?? throw new InvalidOperationException("Services aren't ready yet, load a plugin first.");
        set => StoredServices = value;
    }

    public void Enable(string ass)
    {
        Enable(ass, false);
    }

    public void Enable(string ass, bool fromLoader)
    {
        EnabledAssemblies.Add(ass, true);
        CachedEnabledAssMappings = null;
        CachedDependedAssemblies = null;
        CachedDependedAssMappings = null;
        Preferences.Default.Set("PluginEnabled" + ass, true);


        if (!fromLoader)
        {
            // prevent recursion
            try
            {
                var loaded = AppDomain.CurrentDomain.Load(new AssemblyName(ass));
                if (loaded == null) return;
                // temporary whatever
                StoredAssemblies.TryAdd(ass, loaded);
                _ = TryFindingInterestingTypes(loaded);
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
        Preferences.Default.Set("PluginEnabled" + ass, false);
    }

    private readonly ConcurrentDictionary<string, string> Tried = [];

    internal static Dictionary<Type, Type?> SingleUser { get; } = new()
    {
        {typeof(HttpClient), null },
        {typeof(Lazy<MainLoader?>), null  },
        {typeof(Lazy<Application?>), null },
        {typeof(NavigationManager), null  },
        {typeof(IJSRuntime), null  },
        {typeof(IConfiguration), null  },
        {typeof(IHostEnvironment), null  },
        //{typeof(ILogger<>), typeof(Logger<>)  },
        {typeof(ILoggerFactory), null },

    };
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
            var currents = mappings.SelectMany(BuilderExtensions.GetAssTypesSafely).GetServicable().ToList();
            collection.BuildServices(currents);
        }
        else
            collection.BuildServices(types);
    }


    private async Task RebuildServiceContainer()
    {

        if (IsRebuilding) return;

        IsRebuilding = true; // was trying to decide to put it here or 3 lines down

        try
        {
            // 2. Wait for the "silence" period
            await Task.Delay(400);

            // 3. The actual work
            CachedDependedAssemblies = null;
            CachedEnabledAssMappings = null;
            CachedDependedAssMappings = null;

            var mappings = EnabledAssMappings
                .Concat(DependedAssMappings)
                .Where(ass => !FILTER_MICROSOFT_DLLS_BY_NAME(ass.ToName())) // TODO: this isn't always true?
                .ToList();
            var keys = JsonSerializer.Serialize(mappings.Select(ass => ass.FullName));

            if (keys == previousKeys)
            {
                return;
            }
            previousKeys = keys;

            var collection = new ServiceCollection();

            // TODO: check depended assemblies is empty compared to loaded assemblies then offer an OnSettled even if its preloading is done
            var missing = DependedAssemblies.Where(ass => Tried.ContainsKey(ass.Key) != true).ToList();
            if (missing.Count == 0)
            {


            }
            else
            {
                var parallel = Environment.ProcessorCount - 4;

                var options = new ParallelOptions
                {
                    // Leave at least one or two cores for the UI thread
                    MaxDegreeOfParallelism = Math.Max(1, parallel)
                };

                foreach (var ass in missing) // prevent recursion
                    Tried.TryAdd(ass.Key, ass.Key);

                _ = Parallel.ForEachAsync(missing, options, async (ass, ct) =>
                {
                    try
                    {
                        Assembly.Load(ass.Key);
                    }
                    catch (Exception)
                    {

                    }
                }).ContinueWith(t => RebuildServiceContainer()); // make sure it fires at least once more after we quit below

                IsRebuilding = false;

                return; // might as well duck out now because we know more are coming
            }


            var currents = mappings
                .SelectMany(BuilderExtensions.GetAssTypesSafely)
                .GetServicable()
                .ToList();

            //collection.AddSingleton<IHttpClientFactory>(sp => root.GetRequiredService<IHttpClientFactory>());
            //collection.AddScoped<HttpClient>(sp => Service.GetRequiredService<HttpClient>());
            //collection.AddScoped<AuthenticationStateProvider>(sp => root.GetRequiredService<AuthenticationStateProvider>());

            // TODO: need to check installed and get a list of IHasPlugin.Plugins.Keys would be a list of
            //   all the additional service types and the value is its display options
            List<Type> AlreadyMapped = [];
            var checkExisting = currents.Concat(SingleUser.Keys).ToList(); // add this here so it can be checked below
            foreach (var ass in checkExisting)
            {
                try
                {
                    if (Provider?.GetService(ass) is object serve)
                    {
                        if (ass.Extends(typeof(IHasService)))
                            collection.AddSingleton(ass, sp => serve);
                        else
                            collection.AddScoped(ass, sp => serve);
                        AlreadyMapped.Add(ass);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Who the fuck even are you? " + ex);
                }

            }

            collection.AddCascadingValue(sp => new ErrorBoundary());

            collection.BuildServices(currents, null, AlreadyMapped, true);

            // Finalize the provider
            Services = collection.BuildServiceProvider();

            //var test = Services.GetService<ITitleService>();

            if (Plugin is PluginActivator activator && activator.Services is CompositeServiceProvider composite)
            {
                composite.PluginPopin = Services;
            }

            var old = InternalOnSettled;
            var oldAsync = InternalOnSettledAsync;
            InternalOnSettled = null; // make the fuckers resubscribe anyways, hit only once
            InternalOnSettledAsync = null;
            old?.Invoke();
            _ = oldAsync?.Invoke();


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

    private List<Assembly>? CachedEnabledAssMappings { get; set; } = null;
    internal List<Assembly> EnabledAssMappings
    {
        get => CachedEnabledAssMappings
            ??= [..EnabledAssemblies.Where(kvp => kvp.Value).Select(kvp => kvp.Key)
        .Select(ass => LoadedAssemblies.TryGetValue(ass, out var loaded) ? loaded : null)
        .OfType<Assembly>()];
    }

    // TODO: reset to null on user input
    private Dictionary<string, List<string>>? CachedDependedAssemblies { get; set; } = null;
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

    private List<Assembly>? CachedDependedAssMappings { get; set; } = null;
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


    public List<string> RequiredAssemblies { get; } = [..new List<AssemblyName?>
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
    private string? previousKeys;

    [RequiresAssemblyFiles]
    public ConcurrentDictionary<string, Assembly> LoadedAssemblies
    {
        get
        {
            return StoredAssemblies;
        }
    }


    [RequiresAssemblyFiles]
    public TrustedLoader(
        IComponentActivator? plugin = null,
        ICompositeProvider? provider = null,
        IServiceProvider? service = null)
    {
        Plugin = plugin;
        Service = service;
        Provider = provider;
        //StoredServices ??= _service;
        var asses = AppDomain.CurrentDomain.GetAssemblies();
        var assNames = asses.Select(MetadataReaderExtensions.ToName).ToList();
        var collisions = assNames.GroupBy(n => n).Where(g => g.Count() > 1).ToList();
        if (collisions.Count > 0)
        {
            Console.WriteLine(JsonSerializer.Serialize(assNames));
        }
        lock (StoredAssemblies)
        {
            foreach (var ass in asses)
            {
                StoredAssemblies.TryAdd(ass.ToName(), ass);
            }
        }
        var parallel = Environment.ProcessorCount - 4;

        var options = new ParallelOptions
        {
            // Leave at least one or two cores for the UI thread
            MaxDegreeOfParallelism = Math.Max(1, parallel)
        };

        // this is why i put the Seen gate on this and the file scan
        _ = Parallel.ForEachAsync(asses, options, async (ass, ct) =>
        {
            await TryFindingInterestingTypes(ass);
        });
        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;
        IsBootstrapping = true;
        Task.Run(RunFullScan);


        /*
        // TODO: make contexts unloadable LoadPlugins()
        var context = new AssemblyLoadContext("PluginContext", isCollectible: true);
        var assembly = context.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "Interfacing.dll"));
        */

    }
    public bool IsBootstrapping { get; set; } = true;
    public ConcurrentDictionary<string, PluginContract> DiscoveredStatus { get; } = new();

    //public static List<Type> AllPlugins { get; } = [..Assembly.GetExecutingAssembly().GetTypes()
    //    .Where(t => typeof(IHasPlugins).IsAssignableFrom(t))];

    [RequiresAssemblyFiles("Uses Location for plugin tracking")]
    private void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        var assembly = args.LoadedAssembly;

        // Fallback: If Location is empty (Single File), use the Simple Name
        string location = assembly.Location;
        string title = assembly.ToName();

        StoredAssemblies.TryAdd(title, assembly);

        if (Preferences.Default.Get("PluginEnabled" + title, false))
            Enable(title, true);
        else if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;

        if (!Seen.Contains(assembly))
        {
            Task.Run(async () =>
            {
                await TryFindingInterestingTypes(assembly);
                await RebuildServiceContainer();

                OnAssemblyLoaded?.Invoke(new PluginContract(
                    Title: title,
                    InstallPath: location, // Will be empty string in Single-File
                    IsTrusted: true,
                    Metadata: assembly.GetAssemblyInfo()
                ));
            });
        }
    }


    public readonly List<Type> Layouts = [];
    readonly List<Assembly> Seen = [];
    public readonly List<Assembly> Routable = [];
    public readonly List<Type> CatchAll = [];
    public readonly List<Type> Roots = [];
    public readonly List<Type> AllRoutes = [];

    private async Task TryFindingInterestingTypes(Assembly ass)
    {
        if (Seen.Contains(ass))
            return;

        Seen.Add(ass);

        var allTypes = ass.GetAssTypesSafely();

        var routable = false;

        foreach (var type in allTypes)
        {
            try
            {
                if (typeof(LayoutComponentBase).IsAssignableFrom(type)
                    && type != typeof(LayoutComponentBase))
                    Layouts.Add(type);
                if (type.GetCustomAttributes<RouteAttribute>().FirstOrDefault() is RouteAttribute attr
                    && type != typeof(PluginsPage)) // we already know about ourselves
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

        if (routable)
            Routable.Add(ass);
    }



    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomainOnAssemblyLoad;
        GC.SuppressFinalize(this);
    }



    protected async Task CheckPluginFiles()
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

            if (Preferences.Default.Get("PluginEnabled" + title, false))
            {
                Enable(title);
                if(LoadedAssemblies.TryGetValue(title, out var ass))
                    await TryFindingInterestingTypes(ass);
            }
            else if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;

            OnAssemblyLoaded?.Invoke(new PluginContract(
                Title: title,
                InstallPath: file,
                IsTrusted: false,
                Metadata: new AssemblyInfo("Not Loaded", "", "", Path.GetFileNameWithoutExtension(file), LevelOfTrust.Untrusted)
            ));

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
            DiscoveredStatus.TryAdd(file, contract);

            // Tell the UI to refresh as each item arrives
            OnAssemblyLoaded?.Invoke(contract);

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
                    if (DiscoveredStatus.ContainsKey(file))
                        DiscoveredStatus[file] = newContract;
                    else
                        DiscoveredStatus.TryAdd(file, newContract);
                    OnAssemblyLoaded?.Invoke(newContract);
                }
            }
        });

        IsBootstrapping = false;
    }




    /*

    public static List<Type> EnabledPlugins { get; private set; } = [];

    [RequiresAssemblyFiles()]
    public async Task CheckStatus()
    {
        EnabledPlugins ??= await GetEnabledPlugins(Services);
        foreach (var plugin in EnabledPlugins)
        {
            var newContract = new PluginContract(
                    Title: plugin.Name,
                    InstallPath: plugin.Assembly.Location,
                    IsTrusted: false, //metadata?.IsTrusted ?? false,
                    Metadata: plugin.GetAssemblyInfo()
                );
            DiscoveredStatus.TryAdd(plugin.Assembly.Location, newContract);
            OnAssemblyLoaded?.Invoke(newContract);
        }
    }

    // TODO: use this on service startup? way to bootstrap another container?
    public static async Task<List<Type>> GetEnabledPlugins(IServiceProvider service)
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

    */


    public static bool IsLoading { get; private set; } = false;


    [RequiresAssemblyFiles()]
    private async Task RunFullScan()
    {
        if (IsLoading) return;

        IsLoading = true;
        DiscoveredStatus.Clear();

        await Task.Delay(500);

        // Offload the heavy file IO to a background thread to keep UI snappy
        _ = CheckPluginFiles();

        IsLoading = false;
    }


    private static string[]? PluginFiles { get; set; } = null;
    public string Error { get; private set; }

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
    private readonly IServiceProvider? Service;
    private readonly ICompositeProvider? Provider;

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


    public async Task<LevelOfTrust?> GetTrustedAsync(string filePath, string? expectedPublicKeyToken = null)
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

    public async Task<AssemblyInfo?> GetAssemblyInfoAsync(string filePath, string? expectedPublicKeyToken = null)
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
        if (LoadedAssemblies.TryGetValue(title, out var ass))
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
