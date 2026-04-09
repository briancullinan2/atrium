using System.IO.Pipelines;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Extensions.PrometheusTypes;

public static partial class TypeExtensions
{


    private static readonly ConcurrentBag<Type> _allKnownTypes = [];
    private static readonly HashSet<string> _loadedAssemblies = [];
    private static readonly Lock _loaderLock = new();

    public static List<Type> AllRegisteredTypes { get => [.. _allKnownTypes]; }

    public static List<Type> AllRoutableInterfaces { get; }

    public static List<Type> AllRoutable { get; }

    public static List<MethodInfo> AllRoutes { get; }

    public static bool MineOnly { get; } = true;

    static string? GetProduct(Assembly entry) => entry.GetCustomAttribute<AssemblyProductAttribute>()
        ?.Product;
    static string? GetPackage(Assembly entry) => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.Where(attr => attr.Key.Contains("PackageName")).FirstOrDefault()?.Value;
    static string? GetPublisher(Assembly entry) => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.Where(attr => attr.Key.Contains("PublisherName")).FirstOrDefault()?.Value;
    static string? GetCompany(Assembly entry) =>
        entry.GetCustomAttributes<AssemblyCompanyAttribute>()
        ?.FirstOrDefault()?.Company
        ?? entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.Where(attr => attr.Key.Contains("CompanyName")).FirstOrDefault()?.Value;


    public static IEnumerable<Assembly> GetMine(this IEnumerable<Assembly> asses)
    {
        foreach (var ass in asses)
        {
            if (!ass.IsMine()) continue;
        
            yield return ass;
        }
    }


    //public static List<Assembly> GetMine(this IEnumerable<Type> asses)
    //{
    //    return [.. asses.Where(a => a.Assembly.IsMine()).Select(a => a.Assembly)];
    //}
    public static List<Type> GetMine(this IEnumerable<Type> asses)
    {
        HashSet<Assembly> mine = [.. asses.Select(a => a.Assembly).Distinct().Where(s => s.IsMine())];
        return [..asses.Where(t => mine.Contains(t.Assembly))];
    }


    private static (string Name, string Value) DecodeAttribute(MetadataReader mr, CustomAttribute attr)
    {
        string name = "";
        if (attr.Constructor.Kind == HandleKind.MemberReference)
        {
            var mrf = mr.GetMemberReference((MemberReferenceHandle)attr.Constructor);
            var tr = mr.GetTypeReference((TypeReferenceHandle)mrf.Parent);
            name = mr.GetString(tr.Name);
        }

        var reader = mr.GetBlobReader(attr.Value);

        // Attributes start with a 0x0001 prolog
        if (reader.Length < 4 || reader.ReadUInt16() != 1) return (name, "");

        string val = "";
        try
        {
            // Step 1: Read the compressed length of the first string
            int len = reader.ReadCompressedInteger();
            val = reader.ReadSerializedString() ?? string.Empty; // This is the helper for the actual data

            if (name == "AssemblyMetadataAttribute")
            {
                // Step 2: Read the second string (the Value in the Key/Value pair)
                // Note: We don't need to manually read the length for ReadSerializedString
                // as it handles the compressed integer internally.
                string key = val;
                string data = reader.ReadSerializedString() ?? string.Empty;
                val = $"{key}|{data}";
            }
        }
        catch { /* Not a standard string attribute or empty blob */ }

        return (name, val);
    }



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

    public record AssemblyInfo(string? Product, string? Company, string? Publisher, string? Package);

    public static AssemblyInfo GetAssemblyInfo(this Assembly? entry)
    {
        if(entry == null) return new AssemblyInfo(null, null, null, null);
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

    public static AssemblyInfo GetAssemblyInfo(this MetadataReader? mr)
    {

        if(mr == null) return new AssemblyInfo(null, null, null, null);

        string? product = null, company = null, publisher = null, package = null;

        foreach (var handle in mr.CustomAttributes)
        {
            var attr = mr.GetCustomAttribute(handle);
            var (name, value) = DecodeAttribute(mr, attr);

            switch (name)
            {
                case "AssemblyProductAttribute": product = value; break;
                case "AssemblyCompanyAttribute": company = value; break;
                case "AssemblyMetadataAttribute":
                    // Value for MetadataAttribute is "Key|Value"
                    if (value.StartsWith("PublisherName|")) publisher = value.Split('|')[1];
                    if (value.StartsWith("PackageName|")) package = value.Split('|')[1];
                    if (value.StartsWith("CompanyName|") && company == null) company = value.Split('|')[1];
                    break;
            }
        }
        return new AssemblyInfo(product, company, publisher, package);
    }

    public static List<Type> GetServicable(this IEnumerable<Assembly> asses)
    {
        return asses.ToServices().GetServicable();
    }


    public static List<Type> GetServicable(this IEnumerable<Type> asses)
    {

        List<Type> plugins = [..asses
            .Where(t => t.IsConcrete() && t.Extends(typeof(IHasPlugins)))
            .SelectMany(t => t.GetProperty(nameof(IHasPlugins.Plugins), BindingFlags.Static)?.GetValue(null) as List<Type> ?? [])];

        asses = [..asses.Concat(plugins)];

        List<Type> concrete = [..asses.Where(s => s.IsConcrete())];

        List<string> interfaces = [..asses
            .Where(s => s.IsInterface)
            .Select(i => i.Name)
            ];

        List<Type> servicable = [..concrete
            .Where(c => c.GetInterfaces()
                .Select(i => i.Name)
                .Intersect(interfaces) // Finds names present in both lists
                .Any())                // Returns true if the intersection isn't empty
            ];

        List<Type> currents = [..servicable
            .Where(t => t.Extends(typeof(IHasService<>)))
            .Select(t => {
                var interf = t.GetInterfaces().Where(i => i.Extends(typeof(IHasService)) && i.GenericTypeArguments.Length > 0).First();
                return interf.GetGenericArguments().First();
            })];

        return [..servicable.Concat(currents).Distinct()];
    }


    public static bool IsService(this Type t)
    {
        return t.Name.Contains("Service", StringComparison.InvariantCultureIgnoreCase)
            || t.Namespace?.Contains("Service", StringComparison.InvariantCultureIgnoreCase) == true
            || t.Extends(typeof(IHasService))
            || t.Extends(typeof(IHasPlugins))
            || t.Extends(typeof(IHasFeatures));
    }


    public static List<Type> ToServices(this IEnumerable<Assembly?>? ass)
    {
        if (ass == null) return [];
        return [.. ass.SelectMany(a => a?.ToServices() ?? []).Where(t => t != null)];
    }



    static readonly ConcurrentDictionary<Assembly, List<Type>> _serviceCache = [];
    public static List<Type> ToServices(this Assembly ass)
    {
        if (_serviceCache.TryGetValue(ass, out var services)) return services;
        List<Type> interfaces = [..ass.GetTypes().Where(IsService)];
        List<Type> servicesToCache = [.. interfaces.Concat(ass.GetTypes().Where(interfaces.AnyExtendsAny)).Distinct()];
        _serviceCache.TryAdd(ass, servicesToCache);
        return servicesToCache;
    }

    public static List<Type> ToEntities<TEntity>()
    {
        List<Assembly> ass = [typeof(TEntity).Assembly, .. typeof(TEntity).Assembly.GetAssemblies()];
        return ass.ToEntities<TEntity>();
    }

    public static List<Type> ToEntities<TEntity>(this IEnumerable<Assembly?>? ass)
    {
        RegisterAssembly([.. ass ?? []]);
        return [.._allKnownTypes.Where(t => t.IsClass && !t.IsAbstract
            && t.Extends(typeof(TEntity)) && t.IsConcrete() && t != typeof(object))
            ];
    }



    static TypeExtensions()
    {
        RegisterAssembly([
            Assembly.GetCallingAssembly(), 
            ..Assembly.GetCallingAssembly().GetAssemblies(), 
            Assembly.GetEntryAssembly(), 
            ..Assembly.GetEntryAssembly()?.GetAssemblies() ?? [], 
            Assembly.GetExecutingAssembly()]);

        entry ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        entryDirectory ??= Path.GetDirectoryName(entry.Location);
        product ??= GetProduct(entry);
        package ??= GetPackage(entry);
        publisher ??= GetPublisher(entry);
        company ??= GetCompany(entry);


        AllRoutableInterfaces = [.._allKnownTypes
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.IsInterface)
            ];

        AllRoutable = [.._allKnownTypes
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.GetCustomAttributes<RouteAttribute>().Any())
            ];

        AllRoutes = [.._registeredAssemblies
            .Routes()
            .Distinct()
            ];

        foreach (var type in AllRoutable)
        {
            // This triggers your existing GetRoutes logic to fill the _routeCache
            _ = GetRoutes(type);
        }

    }


    public static void RegisterAssembly(params Assembly?[]? assemblies)
    {
        assemblies = [..(assemblies??[]).Concat(AppDomain.CurrentDomain.GetAssemblies())];

        if (assemblies == null) return;

        foreach (var assembly in assemblies ?? [])
        {
            if (assembly == null) continue;

            if (!_registeredAssemblies.Contains(assembly))
                _registeredAssemblies.Add(assembly);

            var name = assembly.FullName!;

            if (_loadedAssemblies.Contains(name)) continue;

            lock (_loaderLock)
            {
                if (_loadedAssemblies.Add(name))
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        _allKnownTypes.Add(type);
                    }
                }
            }
        }
    }

    private static readonly ConcurrentDictionary<string, Type?> _pathToTypeCache = new();

    private static readonly List<Assembly> _registeredAssemblies = [];
    private static readonly Assembly entry;
    private static readonly string? entryDirectory;
    private static readonly string? product;
    private static readonly string? package;
    private static readonly string? company;
    private static readonly string? publisher;

    public static List<Assembly> GetAssemblies(this Assembly assembly, params Assembly?[]? calling)
    {
        RegisterAssembly([assembly, .. calling ?? []]);
        return [.. _registeredAssemblies];
    }

    public static List<Assembly> GetAssemblies(this AppDomain domain, params Assembly?[]? calling)
    {
        RegisterAssembly([.. domain.GetAssemblies(), .. calling ?? []]);
        return [.. _registeredAssemblies];
    }


    public static List<Assembly> GetAssemblies(params Assembly?[]? calling)
    {
        RegisterAssembly(calling);
        return [.. _registeredAssemblies];
    }






    public static Type? ToType(
        this string filePath, Assembly? targetAssembly = null)
    {
        // 0. Register new assemblies if provided on the fly
        if (targetAssembly != null) RegisterAssembly(targetAssembly ?? Assembly.GetCallingAssembly());

        return _pathToTypeCache.GetOrAdd(filePath, path =>
        {
            // Standardize path separators
            var normalizedPath = path.Replace("\\", "/");
            var fileName = Path.GetFileNameWithoutExtension(normalizedPath);

            // 1. Direct Lookup (In case it's a fully qualified name string)
            var directType = Type.GetType(path);
            if (directType != null) return directType;

            // 2. Exact Name Match (Fuzzy step 1)
            // Filters the master list for anything matching the file name (Component.razor -> Component)
            var potentialMatches = _allKnownTypes
                .Where(t => t.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (potentialMatches.Count == 0) return null;
            if (potentialMatches.Count == 1) return potentialMatches[0];

            // 3. Namespace/Folder Hierarchy Match (Fuzzy step 2)
            // Compares path segments like /Pages/Users/Profile.razor against namespaces
            var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                           .Reverse()
                                           .Skip(1) // Skip the filename itself
                                           .ToList();

            return potentialMatches.OrderByDescending(t =>
            {
                if (string.IsNullOrEmpty(t.Namespace)) return 0;

                // Count how many folders in the path exist in the namespace string
                return pathSegments.Count(segment =>
                    t.Namespace.Contains(segment, StringComparison.OrdinalIgnoreCase));
            })
            .ThenBy(t => t.Namespace?.Length ?? int.MaxValue) // Prefer shorter/closer namespaces if tied
            .FirstOrDefault();
        });
    }
}
