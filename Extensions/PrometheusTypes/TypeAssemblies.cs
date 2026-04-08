namespace Extensions.PrometheusTypes;

public static partial class TypeExtensions
{


    private static readonly ConcurrentBag<Type> _allKnownTypes = [];
    private static readonly HashSet<string> _loadedAssemblies = [];
    private static readonly Lock _loaderLock = new();

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
        var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var entryDirectory = Path.GetDirectoryName(entry.Location);
        var product = GetProduct(entry);
        var package = GetPackage(entry);
        var publisher = GetPublisher(entry);
        var company = GetCompany(entry);

        if (entryDirectory == null) yield break;

        foreach (var ass in asses)
        {
            if (!string.Equals(ass.Location[..Math.Min(entryDirectory.Length, ass.Location.Length)],
                        entryDirectory, StringComparison.InvariantCultureIgnoreCase)) continue;

            if ((product != null && string.Equals(product, GetProduct(ass), StringComparison.InvariantCultureIgnoreCase))

                || (package != null && string.Equals(package, GetPackage(ass), StringComparison.InvariantCultureIgnoreCase))

                || (publisher != null && string.Equals(publisher, GetPublisher(ass), StringComparison.InvariantCultureIgnoreCase))

                || (company != null && string.Equals(company, GetCompany(ass), StringComparison.InvariantCultureIgnoreCase))
            )
        
            yield return ass;
        }
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



    static TypeExtensions()
    {
        RegisterAssembly(Assembly.GetExecutingAssembly());

        // TODO: need a list of servicable types, anything in the namespace Services that has any routes
        var assemblies = Assembly.GetCallingAssembly().GetAssemblies(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());
        //if (MineOnly)
        //{
        //    assemblies = [..assemblies.GetMine()];
        //}

        List<Type> connectedTypes = [..assemblies
            .SelectMany(ass => ass.GetTypes())];

        AllRoutableInterfaces = [..connectedTypes
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.IsInterface)
            ];

        AllRoutable = [..connectedTypes
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.GetCustomAttributes<RouteAttribute>().Any())
            ];

        AllRoutes = [..assemblies
            .Routes()
            .Distinct()
            ];

        foreach (var type in AllRoutable)
        {
            // This triggers your existing GetRoutes logic to fill the _routeCache
            _ = GetRoutes(type);
        }

    }


    public static void RegisterAssembly(params Assembly[]? assemblies)
    {
        if (assemblies == null) return;

        foreach (var assembly in assemblies ?? [])
        {
            if (assembly == null) continue;
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


    public static List<Assembly> GetAssemblies(this Assembly assembly, params Assembly[]? calling)
    {
        return GetAssemblies([assembly, .. calling ?? []]);
    }

    public static List<Assembly> GetAssemblies(this AppDomain domain, params Assembly[]? calling)
    {
        return GetAssemblies([.. domain.GetAssemblies(), .. calling ?? []]);
    }


    public static List<Assembly> GetAssemblies(params Assembly[]? calling)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Concat(calling ?? [])
            .Concat([Assembly.GetExecutingAssembly(),  Assembly.GetCallingAssembly(),
              Assembly.GetEntryAssembly()])
            .Where(a => a != null)
            .ToList();
        foreach (var ass in assemblies)
        {
            if (ass == null) continue;
            if (!_registeredAssemblies.Contains(ass))
                _registeredAssemblies.Add(ass);
        }
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
