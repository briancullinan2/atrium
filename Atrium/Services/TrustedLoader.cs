using Interfacing.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Atrium.Components;
using Microsoft.AspNetCore.Components;



#if WINDOWS
using System.Runtime.InteropServices;
using Atrium.Platforms.Windows;
#endif

namespace Atrium.Services;

public partial class TrustedLoader : ITrustProvider, IHasCurrent<AppDomain>, IDisposable
{
    public static AppDomain Current { get => AppDomain.CurrentDomain; }
    public MainLoader? AttachedMain { get; set; }

    private IServiceProvider? StoredServices = null;
    public IServiceProvider Services {
        get => StoredServices ?? throw new InvalidOperationException("Services aren't ready yet, load a plugin first.");
        set => StoredServices = value; }


    public Dictionary<string, bool> EnabledAssemblies { get; } = [];

    private List<Assembly> EnabledAssMappings
    {
        get
        => [..EnabledAssemblies.Keys
        .Select(ass => LoadedAssemblies.TryGetValue(ass, out var loaded) ? loaded : null)
        .OfType<Assembly>()];
    }

    public Dictionary<string, List<string>> DependedAssemblies
    {
        get => EnabledAssMappings
        .SelectMany(parentAss =>
            parentAss.GetReferencedAssemblies()
                .Select(refAss => new
                {
                    Parent = parentAss.GetName().Name ?? parentAss.GetName().FullName,
                    Dependency = refAss.Name ?? refAss.FullName
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

    public List<string> RequiredAssemblies { get; } = [..new List<AssemblyName?>
        { Assembly.GetEntryAssembly()?.GetName(),
          Assembly.GetExecutingAssembly().GetName(),
        }.Concat(Assembly.GetEntryAssembly()?.GetReferencedAssemblies() ?? [])
        .OfType<AssemblyName>()
        .Select(n => n.Name ?? n.FullName)
        ];

    private static Dictionary<string, Assembly>? StoredAssemblies = null;
    public Dictionary<string, Assembly> LoadedAssemblies
    {
        get => StoredAssemblies ??= AppDomain.CurrentDomain
        .GetAssemblies().ToDictionary(
            ass => Path.GetFileNameWithoutExtension(ass.Location)
            ?? ass.FullName?.Split(',')[0]
            ?? ass.GetName().Name
            ?? ass.GetName().FullName.Split(',')[0],
            ass => ass);
    }


    [RequiresAssemblyFiles]
    public TrustedLoader(IServiceProvider _service, Lazy<MainLoader?> _main)
    {
        AttachedMain = _main.Value;
        StoredServices ??= _service;
        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;
        DiscoveredStatus.Clear();
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
        string title = !string.IsNullOrEmpty(location)
            ? Path.GetFileNameWithoutExtension(location)
            : assembly.GetName().Name ?? "Unknown";

        LoadedAssemblies.TryAdd(title, assembly);

        OnAssemblyLoaded?.Invoke(new PluginContract(
            Title: title,
            InstallPath: location, // Will be empty string in Single-File
            IsTrusted: true,
            Metadata: assembly.GetAssemblyInfo()
        ));

        Task.Run(() => TryFindingInterestingTypes(assembly));
    }


    public readonly List<Type> Layouts = [];
    readonly List<Assembly> Seen = [];
    public readonly List<Assembly> Routable = [];
    public readonly List<Type> CatchAll = [];
    public readonly List<Type> Roots = [];

    private async Task TryFindingInterestingTypes(Assembly ass)
    {
        if (Seen.Contains(ass))
            return;

        Seen.Add(ass);
        var allTypes = ass.GetTypes();
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

        // Process files in parallel batches to speed up metadata extraction
        await Parallel.ForEachAsync(PluginFiles, async (file, ct) =>
        {
            --counter;

            OnAssemblyLoaded?.Invoke(new PluginContract(
                Title: System.IO.Path.GetFileNameWithoutExtension(file),
                InstallPath: file,
                IsTrusted: false,
                Metadata: new AssemblyInfo("Not Loaded", "", "", Path.GetFileNameWithoutExtension(file), LevelOfTrust.None)
            ));

            var trust = await GetTrustedAsync(file);
            if (trust == null) return;

            var contract = new PluginContract(
                Title: System.IO.Path.GetFileNameWithoutExtension(file),
                InstallPath: file,
                IsTrusted: (int)trust.Value > 2,
                Metadata: new AssemblyInfo("Not Loaded", "", "", Path.GetFileNameWithoutExtension(file), trust.Value)
            );

            // Thread-safe update to the UI list
            DiscoveredStatus.TryAdd(file, contract);

            if(counter <= 1) // notify UX
                IsBootstrapping = false;

            // Tell the UI to refresh as each item arrives
            OnAssemblyLoaded?.Invoke(contract);

            if ((int)trust >= (int)LevelOfTrust.Published)
            {
                var meta = await GetAssemblyInfoAsync(file);
                if (meta != null)
                {
                    var newContract = new PluginContract(
                        Title: System.IO.Path.GetFileNameWithoutExtension(file),
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
            var myDelegate = plugin.GetProperty(nameof(IHasPlugins.Installed), BindingFlags.Static)?.GetValue(null) as Delegate;
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
        await Task.Run(async () =>
        {
            var fileTask = CheckPluginFiles();
            //var statusTask = CheckStatus();

            await fileTask; // Task.WhenAll(fileTask, statusTask);
        });

        IsLoading = false;
    }


    private static string[]? PluginFiles { get; set; } = null;

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

    public event Action<PluginContract>? OnAssemblyLoaded;

    public static AssemblyName? VerifyStrongName(string filePath, string? thumbprint = null)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var name = AssemblyName.GetAssemblyName(filePath);
            var token = name.GetPublicKeyToken();

            if (token == null || token.Length == 0) return null;

            if(thumbprint != null
                && string.Equals(Convert.ToHexString(token), thumbprint, StringComparison.InvariantCultureIgnoreCase))
                return name;

            if (Whitelist.Contains(Convert.ToHexString(token)))
                return name;
            return null;
        }
        catch
        {
            // Likely a native DLL or corrupt file
            return null;
        }
    }

    public void SetPermission(Type? PermissionType) => AttachedMain?.PermissionType = PermissionType;
    public void SetNotFound(Type? NotFoundControl) => AttachedMain?.NotFoundControl = NotFoundControl;
    public void SetAuthWrapper(Type? AuthWrapper) => AttachedMain?.AuthWrapper = AuthWrapper;
    public void SetDefaultLayout(Type? DefaultLayout) => AttachedMain?.DefaultLayout = DefaultLayout;
    public void SetAppAssembly(Assembly? AppAssembly) => AttachedMain?.AppAssembly = AppAssembly;

    /*
    public static bool VerifyCertificate(string filePath, string? thumbprint = null)
    {
        try
        {
            
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
        LevelOfTrust level;
        if (VerifyStrongName(filePath, expectedPublicKeyToken) != null)
            level = LevelOfTrust.Published;
        else
            return null;

        // TODO: fix flow for this
        if (expectedPublicKeyToken == null)
            level = LevelOfTrust.Mine;

#if WINDOWS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            if (VerifyWindowsSignature(filePath, expectedPublicKeyToken))
                level = LevelOfTrust.Verified;
#endif


        //if (VerifyCertificate(filePath, expectedPublicKeyToken))
        //    level = LevelOfTrust.Signed;

        return level;
    }

    public async Task<AssemblyInfo?> GetAssemblyInfoAsync(string filePath, string? expectedPublicKeyToken = null)
    {
        var level = await GetTrustedAsync(filePath);

        if(level == null)
            return new AssemblyInfo(
                "Not Trustable",
                null,
                null,
                Path.GetFileNameWithoutExtension(filePath),
                LevelOfTrust.None
            );

        if (level < LevelOfTrust.Published)
            return new AssemblyInfo(
                "Not Loaded",
                null,
                null,
                Path.GetFileNameWithoutExtension(filePath),
                level.Value
            );

        // TODO: temporary
        /*if (TypeExtensions.AllAssemblies
            .FirstOrDefault(a => string.Equals(a.Location ?? System.AppContext.BaseDirectory, filePath, StringComparison.InvariantCultureIgnoreCase))
            is Assembly ass)
            meta = new AssemblyInfo(
                ass.GetProduct(),
                ass.GetCompany(),
                ass.GetPublisher(),
                ass.GetPackage(),
                level.Value
            );
        else*/

        AssemblyInfo? meta = MetadataReaderExtensions.GetAssemblyInfo(filePath);
        if (meta == null) return new AssemblyInfo(
            "No Metadata",
            "",
            "",
            Path.GetFileNameWithoutExtension(filePath),
            level.Value
        );

        if (meta.IsMine())
            level = LevelOfTrust.Mine;


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
