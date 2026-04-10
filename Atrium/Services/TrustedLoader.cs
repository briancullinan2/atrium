using Interfacing.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

#if WINDOWS
using System.Runtime.InteropServices;
using Atrium.Platforms.Windows;
#endif

namespace Atrium.Services;

public partial class TrustedLoader : ITrustProvider, IHasCurrent<AppDomain>, IDisposable
{
    public static AppDomain Current { get => AppDomain.CurrentDomain; }

    private IServiceProvider? StoredServices = null;
    public IServiceProvider Services {
        get => StoredServices ?? throw new InvalidOperationException("Services aren't ready yet, load a plugin first.");
        set => StoredServices = value; }

    [RequiresAssemblyFiles]
    public TrustedLoader(IServiceProvider _service)
    {
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

    public static List<Type> AllPlugins { get; } = [..Assembly.GetExecutingAssembly().GetTypes()
        .Where(t => typeof(IHasPlugins).IsAssignableFrom(t))];

    [RequiresAssemblyFiles("Uses Location for plugin tracking")]
    private void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        var assembly = args.LoadedAssembly;

        // Fallback: If Location is empty (Single File), use the Simple Name
        string location = assembly.Location;
        string title = !string.IsNullOrEmpty(location)
            ? Path.GetFileNameWithoutExtension(location)
            : assembly.GetName().Name ?? "Unknown";

        OnAssemblyLoaded?.Invoke(new PluginContract(
            Title: title,
            InstallPath: location, // Will be empty string in Single-File
            IsTrusted: true,
            Metadata: assembly.GetAssemblyInfo()
        ));
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
            var statusTask = CheckStatus();

            await Task.WhenAll(fileTask, statusTask);
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


internal static class MetadataReaderExtensions
{
    public static AssemblyInfo? GetAssemblyInfo(string filePath)
    {
        try
        {
            if (filePath == null) return null;
            if (File.Exists(filePath) == false) return null;
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);
            var metadataReader = peReader.GetMetadataReader();

            return metadataReader.MetaAttributeNameValueMatch();
        }
        catch (Exception)
        {
            return null;
        }
    }


    public static AssemblyInfo MetaAttributeNameValueMatch(this MetadataReader? mr)
    {
        if (mr == null) return new AssemblyInfo(null, null, null, null);

        string? product = null, company = null, publisher = null, package = null, authors = null;


        foreach (var handle in mr.CustomAttributes)
        {
            var attr = mr.GetCustomAttribute(handle);
            var (name, value) = DecodeAttribute(mr, attr);

            switch (name)
            {
                case "AssemblyProductAttribute":
                    product = value;
                    break;
                case "AssemblyCompanyAttribute":
                    company = value;
                    break;
                case "AssemblyMetadataAttribute":
                    // Metadata attributes are Key/Value pairs. 
                    // Ensure DecodeAttribute handles the Key|Value format correctly.
                    var parts = value.Split('|');
                    if (parts.Length < 2) break;

                    var key = parts[0];
                    var val = parts[1];

                    if (key == "PublisherName") publisher = val;
                    if (key == "PackageName") package = val;
                    // Catch "Authors" if you stored it in metadata
                    if (key == "Authors") authors = val;
                    if (key == "CompanyName" && company == null) company = val;
                    break;
            }
        }
        return new AssemblyInfo(product, company, publisher + authors, package);
    }



    private static (string Name, string Value) DecodeAttribute(MetadataReader mr, CustomAttribute attr)
    {
        string name = "";
        if (attr.Constructor.Kind == HandleKind.MemberReference)
        {
            var mrf = mr.GetMemberReference((MemberReferenceHandle)attr.Constructor);
            if (mrf.Parent.Kind == HandleKind.TypeReference)
            {
                var tr = mr.GetTypeReference((TypeReferenceHandle)mrf.Parent);
                name = mr.GetString(tr.Name);
            }
        }

        // --- DUCK OUT EARLY ---
        // List only the attributes we actually care about in the switch/extension methods
        if (name != "AssemblyProductAttribute" &&
            name != "AssemblyCompanyAttribute" &&
            name != "AssemblyMetadataAttribute" &&
            name != "AssemblyTitleAttribute" &&
            name != "AssemblyDescriptionAttribute")
        {
            return (name, ""); // Return the name so the loop knows we saw it, but skip the value
        }

        var reader = mr.GetBlobReader(attr.Value);

        // Attributes start with a 0x0001 prolog
        if (reader.Length < 4 || reader.ReadUInt16() != 1) return (name, "");

        string val = "";
        try
        {
            // Read the first argument
            val = reader.ReadSerializedString() ?? string.Empty;

            if (name == "AssemblyMetadataAttribute")
            {
                // Read the second argument (the Value in the Key/Value pair)
                string key = val;
                string data = reader.ReadSerializedString() ?? string.Empty;
                val = $"{key}|{data}";
            }
        }
        catch { /* Metadata is malformed or not a simple string attribute */ }


        return (name, val);
    }


    private static readonly Assembly entry;
    private static readonly string? entryDirectory;
    private static readonly string? product;
    private static readonly string? package;
    private static readonly string? company;
    private static readonly string? publisher;

    static MetadataReaderExtensions()
    {
        entry ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        entryDirectory ??= Path.GetDirectoryName(AppContext.BaseDirectory);
        product ??= GetProduct(entry);
        package ??= GetPackage(entry);
        publisher ??= GetPublisher(entry);
        company ??= GetCompany(entry);

    }


    public static bool IsMine(this AssemblyInfo ass)
    {

        if (entryDirectory == null) return false;


        if ((product != null && string.Equals(product, ass.Product, StringComparison.InvariantCultureIgnoreCase))

            || (package != null && string.Equals(package, ass.Package, StringComparison.InvariantCultureIgnoreCase))

            || (publisher != null && string.Equals(publisher, ass.Publisher, StringComparison.InvariantCultureIgnoreCase))

            || (company != null && string.Equals(company, ass.Company, StringComparison.InvariantCultureIgnoreCase))
        ) return true;

        return false;

    }

    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    public static bool IsMine(this Assembly ass)
    {

        if (entryDirectory == null) return false;

        if (!string.Equals(ass.Location[..Math.Min(entryDirectory.Length, ass.Location.Length)],
                        entryDirectory, StringComparison.InvariantCultureIgnoreCase)) return false;

        if ((product != null && string.Equals(product, GetProduct(ass), StringComparison.InvariantCultureIgnoreCase))

            || (package != null && string.Equals(package, GetPackage(ass), StringComparison.InvariantCultureIgnoreCase))

            || (publisher != null && string.Equals(publisher, GetPublisher(ass), StringComparison.InvariantCultureIgnoreCase))

            || (company != null && string.Equals(company, GetCompany(ass), StringComparison.InvariantCultureIgnoreCase))
        )
            return true;

        return false;
    }



    public static string? GetProduct(this Assembly entry)
        => entry.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? entry.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

    public static string? GetPackage(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "PackageName" || attr.Key == "PackageId")?.Value
        ?? entry.GetName().Name; // Fallback to the actual DLL name

    public static string? GetPublisher(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "PublisherName" || attr.Key == "Authors" || attr.Key == "Owner")?.Value
        ?? entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

    public static string? GetCompany(this Assembly entry)
        => entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
        ?? entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "CompanyName" || attr.Key == "Organization")?.Value;


    public static AssemblyInfo GetAssemblyInfo(this Assembly? entry)
    {
        if (entry == null) return new AssemblyInfo(null, null, null, null);
        var product = GetProduct(entry);
        var package = GetPackage(entry);
        var publisher = GetPublisher(entry);
        var company = GetCompany(entry);
        return new AssemblyInfo(product, company, publisher, package);
    }

    public static AssemblyInfo GetAssemblyInfo(this Type? entry)
    {
        return entry?.Assembly.GetAssemblyInfo() ?? new AssemblyInfo(null, null, null, null);
    }

    public static object? InvokeService(this Delegate? myDelegate, IServiceProvider service, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        return myDelegate.Method.InvokeService(service, myDelegate.Target, args);
    }

    public static object? InvokeService(this MethodInfo? myDelegate, IServiceProvider service, object? thisObject = null, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        var formFactor = service.GetService(typeof(IFormFactor)) as IFormFactor;
        var parameters = myDelegate.GetParameters();
        var parameterValues = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var realType = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
            // TODO: add more special service injection handlers
            //if (parameters[i].ParameterType == typeof(Type) && string.Equals(parameters[i].Name, "routeControl"))
            //{
            //    var nav = service.GetRequiredService<NavigationManager>();
            //    parameterValues[i] = IdentifyNavigation(nav.Uri).ComponentType;
            //}
            //else 
            if (args?.ElementAtOrDefault(i) == null && Nullable.GetUnderlyingType(parameters[i].ParameterType) != null)
            {
                parameterValues[i] = null;
            }
            // TODO: find a way to match parameter names to a dictionary passed in or query params?
            else if (args?.ElementAtOrDefault(i) is object obj
                && realType.IsAssignableFrom(obj.GetType()))
            {
                parameterValues[i] = Convert.ChangeType(obj, realType);
            }
            else if (args?.FirstOrDefault(a => realType.IsAssignableFrom(a?.GetType()) == true) is object obj2)
            {
                parameterValues[i] = Convert.ChangeType(obj2, realType);
            }
            else if (!string.IsNullOrEmpty(parameters[i].Name)
                && formFactor?.QueryParameters?.ContainsKey(parameters[i].Name!) is object queryParameter)
            {
                parameterValues[i] = Convert.ChangeType(queryParameter, realType);
            }
            else
            {
                parameterValues[i] = service.GetService(realType);
            }
        }

        if (thisObject != null && !myDelegate.IsStatic)
        {
            return myDelegate.Invoke(thisObject, parameterValues);
        }
        return myDelegate.Invoke(null, thisObject != null ? [thisObject, .. parameterValues] : parameterValues);
    }
}
