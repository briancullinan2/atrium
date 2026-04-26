
#if !BROWSER
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Storage;

#endif

using Atrium.Components;
using System.ComponentModel;
using Interfacing.Services;

#if WINDOWS
using System.Runtime.InteropServices;
using Atrium.Platforms.Windows;
#endif

using System.Text.Json.Serialization;

namespace Atrium.Services;


public partial class TrustedLoader : ITrustProvider, IHasCurrent<AppDomain>, IDisposable
{
    internal static readonly TrustedState CachedState = new(null);
    internal static readonly TrustedState WorkingState = new(null);



    public TrustedState State
    {
        get
        {
            var copy = new TrustedState(null);
            copy.RestoreState(CachedState.SetState());
            return copy;
        }
    }
    [JsonIgnore]
    public static AppDomain Current { get; set; } = AppDomain.CurrentDomain;

    // TODO: move this event into the state since it could be tied to a page
    //   then call the event delegate inside every trustedstate subscribed to this instance?
    private static Delegate? InternalOnSettled;
    public event Action? OnSettled
    {
        add
        {
            if (value == null) return;
            // incase anything forgets this bool setOnce = false pattern
            if (InternalOnSettled?.GetInvocationList().Contains(value) == true) return;
            InternalOnSettled = Delegate.Combine(InternalOnSettled, value);
            if (WorkingState.IsRebuilding || WorkingState.IsBootstrapping) return; // allow subscribers in without immediately retriggering
            _ = SettleServices(null);
        }
        remove
        {
            InternalOnSettled = Delegate.Remove(InternalOnSettled, value);
        }
    }

    // TODO: use a ConcurrentDictionary and actually await the calls?
    // fires at least once for everybody even if services aren't rebuilt
    public void Subscribe(Delegate? value)
    {
        if (value == null) return;
        // incase anything forgets this bool setOnce = false pattern
        if (InternalOnSettled?.GetInvocationList().Contains(value) == true) return;
        InternalOnSettled = Delegate.Combine(InternalOnSettled, value);
        if (WorkingState.IsRebuilding || WorkingState.IsBootstrapping) return; // allow subscribers in without immediately retriggering
        _ = SettleServices(null);
    }

    public void Unsubscribe(Delegate? value)
    {
        InternalOnSettled = Delegate.Remove(InternalOnSettled, value);
    }


    private static readonly Func<string?, bool> FILTER_MICROSOFT_DLLS_BY_NAME;



    static readonly Type? Program;

    static TrustedLoader()
    {
        FILTER_MICROSOFT_DLLS_BY_NAME = title => string.IsNullOrEmpty(title) || title.StartsWith("System.") || title.StartsWith("Microsoft.") || title.StartsWith("WinRT.");

        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;

        ReloadAppDomain();

        lock (CachedState)
            Program = CachedState.Programs.FirstOrDefault();
    }


    public void Enable(string ass)
    {
        if (WorkingState.EnabledAssemblies.ContainsKey(ass))
            WorkingState.EnabledAssemblies[ass] = true;
        else
            WorkingState.EnabledAssemblies.TryAdd(ass, true);
        lock (CachedState)
            if (CachedState.EnabledAssemblies.ContainsKey(ass))
                CachedState.EnabledAssemblies[ass] = true;
            else
                CachedState.EnabledAssemblies.TryAdd(ass, true);
        Enable(ass, false);
    }

    public static void Enable(string ass, bool fromLoader)
    {
        WorkingState.SystemEnabledAssemblies.TryAdd(ass, true);
        lock (CachedState)
        {
            CachedState.SystemEnabledAssemblies.TryAdd(ass, true);
            CachedState.EnabledAssMappings = null;
            CachedState.DependedAssemblies = null;
            CachedState.DependedAssMappings = null;
            CachedState.RequiredAssMappings = null;
        }
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
                lock(WorkingState)
                    WorkingState.LoadedAssemblies.TryAdd(ass, loaded);
                lock(CachedState)
                    CachedState.LoadedAssemblies.TryAdd(ass, loaded);
                TryFindingInterestingTypes(loaded);
            }
            catch (Exception ex)
            {
                // do something statusy
                WorkingState.Error = ex.Message;
                lock(CachedState)
                    CachedState.Error = ex.Message;
            }
        }
    }

    public void Disable(string ass)
    {
        WorkingState.EnabledAssemblies.Remove(ass);
        lock (CachedState)
        {
            CachedState.EnabledAssemblies.Remove(ass);
            CachedState.EnabledAssMappings = null;
            CachedState.DependedAssemblies = null;
            CachedState.DependedAssMappings = null;
            CachedState.RequiredAssMappings = null;
        }

#if !BROWSER
        Preferences.Default.Set("PluginEnabled" + ass, false);
#endif
    }


    static readonly ConcurrentDictionary<string, string> Tried = [];
    static readonly SemaphoreSlim _entry = new(1, 1);
    static readonly List<Assembly> PileUp = [];

    private static async Task SettleServices(Assembly? newAss)
    {
        try
        {
            if(newAss != null)
                PileUp.Add(newAss);

            await Task.Delay(300);

            lock (_entry)
            {
                if (WorkingState.IsRebuilding) return;

                WorkingState.IsRebuilding = true; // was trying to decide to put it here or 3 lines down
                lock (CachedState)
                {
                    CachedState.IsRebuilding = true;
                    CachedState.DependedAssemblies = null;
                    CachedState.EnabledAssMappings = null;
                    CachedState.DependedAssMappings = null;
                    CachedState.RequiredAssMappings = null;
                }
            }




            var mappings = PileUp.SelectMany(ass => ass.GetReferencedAssemblies())
                .Select(ass => ass.ToName())
                .Where(ass => !FILTER_MICROSOFT_DLLS_BY_NAME(ass))
                .ToList();
            PileUp.Clear();


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


                return; // might as well duck out now because we know more are coming
            }

            lock (CachedState)
            {
                var old = InternalOnSettled;
                InternalOnSettled = null; // make the fuckers resubscribe anyways, hit only once
                foreach(var invocation in InternalOnSettled?.GetInvocationList() ?? [])
                {
                    try
                    {
#if !BROWSER
                        invocation.InvokeService(MauiProgram.Current.Services);
#else
                        invocation.InvokeService(Program?.GetProperty(nameof(IHasProgram.Service), BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as IServiceProvider);
#endif
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                CachedState.IsRebuilding = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            lock (_entry)
            {
                WorkingState.IsRebuilding = false;
            }
        }
    }

    internal static List<Assembly> EnabledAssMappings
    {
        get
        {
            lock(CachedState)
            {
                // don't need to touch working state here, these are updated intentionally both places with locks
                return CachedState.EnabledAssMappings ??= [..CachedState.EnabledAssemblies.Where(kvp => kvp.Value).Select(kvp => kvp.Key)
                .Concat(CachedState.SystemEnabledAssemblies.Where(kvp => kvp.Value).Select(kvp => kvp.Key))
                .Select(ass => CachedState.LoadedAssemblies.TryGetValue(ass, out var loaded) ? loaded : null)
                .OfType<Assembly>()];
            }
        }
    }

    // TODO: reset to null on user input
    public static Dictionary<string, List<string>> DependedAssemblies
    {
        get
        {
            lock (CachedState)
            {
                return CachedState.DependedAssemblies ??= TrustedLoader.EnabledAssMappings
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

        }
    }

    public static List<Assembly> DependedAssMappings
    {
        get
        {
            lock (CachedState)
            {
                return CachedState.DependedAssMappings
                    ??= [..RequiredAssMappings, ..global::Atrium.Services.TrustedLoader.EnabledAssMappings
                .Concat(RequiredAssMappings)
                .SelectMany(parentAss => parentAss.GetReferencedAssemblies())
                .Select(ass => {
                    if (CachedState.LoadedAssemblies.TryGetValue(ass.ToName(), out var loaded) == true) return loaded;
                    return null;
                })
                .OfType<Assembly>()
                .Distinct()
                ];
            }
        }
    }


    public static List<string> RequiredAssemblies { get; } = [..new List<AssemblyName?>
        { Assembly.GetEntryAssembly()?.GetName(),
          Assembly.GetExecutingAssembly().GetName(),
        }.Concat((Assembly.GetEntryAssembly()??Assembly.GetExecutingAssembly()).GetReferencedAssemblies() ?? [])
        .OfType<AssemblyName>()
        .Select(MetadataReaderExtensions.ToName)
        ];

    public static List<Assembly> RequiredAssMappings
    {
        get
        {
            lock (CachedState)
            {
                return CachedState.RequiredAssMappings ??= [..RequiredAssemblies
                .Select(ass => {
                    if (CachedState.LoadedAssemblies.TryGetValue(ass, out var loaded) == true) return loaded;
                    return null;
                })
                .OfType<Assembly>()
                .Distinct()
                ];
            }
        }
    }





    [RequiresAssemblyFiles]
    public TrustedLoader()
    {
        var parallel = Environment.ProcessorCount - 4;

        var options = new ParallelOptions
        {
            // Leave at least one or two cores for the UI thread
            MaxDegreeOfParallelism = Math.Max(1, parallel)
        };

        lock(WorkingState)
            WorkingState.IsBootstrapping = true;
        lock (CachedState)
            CachedState.IsBootstrapping = true;

        _ = CheckPluginFiles();

        List<Assembly>? copy = null;
        lock (WorkingState)
            copy = [..WorkingState.LoadedAssemblies.Values];

        _ = Parallel.ForEachAsync(copy ?? [], options, async (ass, ct) =>
        {
            // this is why i put the Seen gate on this and the file scan
            string title = ass.ToName();
            if (title.Contains("RazorSharp") == true)
            {
                Console.WriteLine(title);
            }
            if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;

            var contract = new PluginContract(
                Title: title,
                InstallPath: ass.Location, // Will be empty string in Single-File
                IsTrusted: true,
                Metadata: ass.GetAssemblyInfo()
            );

            lock (CachedState)
            {
                CachedState.DiscoveredStatus.TryAdd(title, contract);
                CachedState.Scanning = ass.Location;
            }
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


    

    [RequiresAssemblyFiles("Uses Location for plugin tracking")]
    private static void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        var assembly = args.LoadedAssembly;

        // Fallback: If Location is empty (Single File), use the Simple Name
        string location = assembly.Location;
        string title = assembly.ToName();
        if (title.Contains("Hosting") == true)
        {
            Console.WriteLine(title);
        }

        Console.WriteLine("Assembly loaded: " + title + " : " + location);
        lock(WorkingState)
            WorkingState.LoadedAssemblies.TryAdd(title, assembly);
        lock(CachedState)
            CachedState.LoadedAssemblies.TryAdd(title, assembly);

#if false
#if !BROWSER
        if (Preferences.Default.Get("PluginEnabled" + title, false))
            Enable(title, true);
        else
#endif
#endif
            if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) return;

        Task.Run(async () =>
        {
            TryFindingInterestingTypes(assembly);

            var contract = new PluginContract(
                Title: title,
                InstallPath: location, // Will be empty string in Single-File
                IsTrusted: true,
                Metadata: assembly.GetAssemblyInfo()
            );

            lock(CachedState)
                CachedState.DiscoveredStatus.TryAdd(title, contract);
            PrivateAssemblyLoaded?.Invoke(contract);
            await SettleServices(assembly);
        });

    }


    private static void TryFindingInterestingTypes(Assembly ass)
    {
        if (ass.FullName?.Contains("RazorSharp") == true)
        {
            Console.WriteLine(ass.ToName());
        }
        lock (CachedState)
        {
            if (CachedState.Seen.Contains(ass))
                return;

            CachedState.Seen.Add(ass);
        }

        if (ass.IsMine())
        {
            Console.WriteLine(ass.ToName());
        }

        var allTypes = ass.GetAssTypesSafely();

        var routable = false;


        lock (CachedState)
            foreach (var type in allTypes)
            {
                try
                {

                    if (typeof(LayoutComponentBase).IsAssignableFrom(type)
                        && type != typeof(LayoutComponentBase)
                        && !CachedState.Layouts.Contains(type))
                        CachedState.Layouts.Add(type);

                    if (typeof(IHasPlugins).IsAssignableFrom(type)
                        && !CachedState.AllPlugins.Contains(type))
                        CachedState.AllPlugins.Add(type);

                    if (type.IsServiceable() && !CachedState.StoredServiceable.ContainsKey(type))
                        CachedState.StoredServiceable.TryAdd(type, [.. type.GetInterfaces()]);

                    if (type.GetCustomAttributes<RouteAttribute>().FirstOrDefault() is RouteAttribute attr
                        && type != typeof(Atrium.Components.PluginsPage)
                        && !CachedState.AllRoutes.Contains(type)) // we already know about ourselves
                    {
                        routable = true;
                        if (attr.Template.StartsWith("/*")
                            || attr.Template.StartsWith("/{*")
                            || attr.Template.StartsWith('*'))
                            CachedState.CatchAll.Add(type);

                        if (attr.Template == "/")
                            CachedState.Roots.Add(type);

                        CachedState.AllRoutes.Add(type);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

        if (routable && !CachedState.Routable.Contains(ass))
            CachedState.Routable.Add(ass);
    }



    public void Dispose()
    {
        PrivateAssemblyLoaded -= TrustedLoader_PrivateAssemblyLoaded;
        GC.SuppressFinalize(this);
    }



    protected static async Task CheckPluginFiles()
    {
        // TODO: refresh button
        CachedState.PluginFiles ??= Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        var counter = CachedState.PluginFiles.Length;
        var parallel = Environment.ProcessorCount - 4;

        var options = new ParallelOptions
        {
            // Leave at least one or two cores for the UI thread
            MaxDegreeOfParallelism = Math.Max(1, parallel)
        };

        // Process files in parallel batches to speed up metadata extraction
        await Parallel.ForEachAsync(CachedState.PluginFiles, options, async (file, ct) =>
        {
            --counter;

            if (counter <= 1)
            {
                // notify UX
                lock(WorkingState)
                    WorkingState.IsBootstrapping = false;
                lock (CachedState)
                    CachedState.IsBootstrapping = false;
            }

            var title = Path.GetFileNameWithoutExtension(file);
            if (title.Contains("RazorSharp") == true)
            {
                Console.WriteLine(title);
            }
            if (title.Contains("Hosting") == true)
            {
                Console.WriteLine(title);
            }

#if !BROWSER
            if (Preferences.Default.Get("PluginEnabled" + title, false))
            {
                Enable(title, false);
                Assembly? ass = null;
                lock (CachedState)
                    CachedState.LoadedAssemblies.TryGetValue(title, out ass);

                if (ass != null)
                    TryFindingInterestingTypes(ass);
                else
                    Console.WriteLine("Assembly enabled but types not loaded: " + title);
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
            lock(CachedState)
                CachedState.DiscoveredStatus.TryAdd(title, unloadedContract);
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
            lock(CachedState)
                if (CachedState.DiscoveredStatus.ContainsKey(title))
                    CachedState.DiscoveredStatus[title] = contract;
                else
                    CachedState.DiscoveredStatus.TryAdd(title, contract);

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
                    lock (CachedState)
                        if (CachedState.DiscoveredStatus.ContainsKey(title))
                            CachedState.DiscoveredStatus[title] = newContract;
                        else
                            CachedState.DiscoveredStatus.TryAdd(title, newContract);
                    PrivateAssemblyLoaded?.Invoke(newContract);
                }
            }
        });

        lock(WorkingState)
            WorkingState.IsBootstrapping = false;
        lock (CachedState)
            CachedState.IsBootstrapping = false;
        await SettleServices(null);
    }



    

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
                lock(WorkingState)
                    WorkingState.LoadedAssemblies.TryAdd(title, ass);
                lock (CachedState)
                    CachedState.LoadedAssemblies.TryAdd(title, ass);
                if (FILTER_MICROSOFT_DLLS_BY_NAME(title)) continue;
                if (title.Contains("RazorSharp") == true)
                {
                    Console.WriteLine(title);
                }
                TryFindingInterestingTypes(ass);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }


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
        lock(WorkingState)
            if (WorkingState.LoadedAssemblies.TryGetValue(title, out var ass))
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

