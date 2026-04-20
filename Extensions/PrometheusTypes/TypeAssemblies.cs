
using System.Xml.Linq;

namespace Extensions.PrometheusTypes;

public static partial class TypeExtensions
{
    public static bool StaticMatchRouteAttribute (Attribute a) => a.GetType().Name.Contains("Route");
    public static bool StaticMatchQueryAttribute(Attribute a) => a.GetType().Name.Contains("ParameterFromQuery");
    public static bool StaticMatchAnonymousAttribute (Attribute a) => a.GetType().Name.Contains("AllowAnonymous");
    public static bool StaticMatchAuthorizeAttribute (Attribute a) => a.GetType().Name.Contains("AuthorizeAttribute");
    public static bool StaticMatchParameterAttribute (Attribute a) => a.GetType().Name.Contains("ParameterAttribute");
    public static bool StaticMatchRouteAttribute (CustomAttributeData a) => a.AttributeType.Name.Contains("Route");
    public static bool StaticMatchQueryAttribute (CustomAttributeData a) => a.AttributeType.Name.Contains("ParameterFromQuery");
    public static bool StaticMatchAnonymousAttribute (CustomAttributeData a) => a.AttributeType.Name.Contains("AllowAnonymous");
    public static bool StaticMatchAuthorizeAttribute (CustomAttributeData a) => a.AttributeType.Name.Contains("AuthorizeAttribute");
    public static bool StaticMatchParameterAttribute (CustomAttributeData a) => a.AttributeType.Name.Contains("ParameterAttribute");


    private static readonly ConcurrentBag<Type> _allKnownTypes = [];
    private static readonly ConcurrentBag<Type> _allMineTypes = [];
    private static readonly HashSet<string> _loadedAssemblies = [];
    private static readonly Lock _loaderLock = new();

    public static List<Type> AllRegisteredTypes { get => [.. _allKnownTypes]; }

    public static List<Type> MyRoutableInterfaces { get; private set; } = [];

    public static List<Type> MyRoutable { get; private set; } = [];

    public static List<MethodInfo> MyRoutes { get; private set; } = [];

    public static bool MineOnly { get; } = true;

    public static string? GetProduct(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyProductAttribute>().FirstOrDefault()?.Product
        ?? entry.GetCustomAttributes<AssemblyTitleAttribute>().FirstOrDefault()?.Title;

    public static string? GetPackage(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "PackageName" || attr.Key == "PackageId")?.Value
        ?? entry.GetName().Name; // Fallback to the actual DLL name

    public static string? GetPublisher(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "PublisherName" || attr.Key == "Authors" || attr.Key == "Owner")?.Value
        ?? entry.GetCustomAttributes<AssemblyCompanyAttribute>().FirstOrDefault()?.Company;

    public static string? GetCompany(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyCompanyAttribute>().FirstOrDefault()?.Company
        ?? entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "CompanyName" || attr.Key == "Organization")?.Value;


    public static IEnumerable<Assembly> GetMine(this IEnumerable<Assembly> asses)
    {
        foreach (var ass in asses)
        {
            if (!ass.IsMine()) continue;

            yield return ass;
        }
    }


    public static List<Type> GetMine(this IEnumerable<Type> types)
    {
        // Local cache to store results of IsMine() for this execution
        var checkedAssemblies = new Dictionary<Assembly, bool>();

        return [..types.Where(t =>
        {
            var asm = t.Assembly;
            if (!checkedAssemblies.TryGetValue(asm, out bool isMine))
            {
                isMine = asm.IsMine();
                checkedAssemblies[asm] = isMine;
            }
            return isMine;
        })];
    }



    public static string ToName(this Assembly? ass)
    {
        if (ass == null) return string.Empty;
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


    public static bool IsMine(this Assembly ass)
    {

        //Console.WriteLine("Product: " + entryDirectory + ", " + ass.Location + ", " + GetProduct(ass));

        // doesn't work on web as promised by compiler
        //if (entryDirectory == null) return false;


        if (!string.IsNullOrWhiteSpace(entryDirectory)
            && !string.IsNullOrWhiteSpace(ass.Location)
            && !string.Equals(ass.Location[..Math.Min(entryDirectory.Length, ass.Location.Length)],
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

        //if (entryDirectory == null) return false;


        if ((product != null && string.Equals(product, ass.Product, StringComparison.InvariantCultureIgnoreCase))

            || (package != null && string.Equals(package, ass.Package, StringComparison.InvariantCultureIgnoreCase))

            || (publisher != null && string.Equals(publisher, ass.Publisher, StringComparison.InvariantCultureIgnoreCase))

            || (company != null && string.Equals(company, ass.Company, StringComparison.InvariantCultureIgnoreCase))
        ) return true;

        return false;

    }

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




    public static List<Type> ToEntities<TInterface, TAssembly>()
    {
        return [..typeof(TAssembly).Assembly.GetAssTypesSafely().Where(t => t.IsClass && !t.IsAbstract
            && t.Extends(typeof(TInterface)) && t.IsConcrete() && t != typeof(object))
            ];
    }

    public static List<Type> ToEntities<TEntity>(this IEnumerable<Assembly?>? ass)
    {
        RegisterAssembly([.. ass ?? []]);
        return [.._allMineTypes.Where(t => t.IsClass && !t.IsAbstract
            && t.Extends(typeof(TEntity)) && t.IsConcrete() && t != typeof(object))
            ];
    }


    static TypeExtensions()
    {

        entry ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        entryDirectory ??= Path.GetDirectoryName(entry.Location) ?? AppContext.BaseDirectory;
        product ??= GetProduct(entry);
        package ??= GetPackage(entry);
        publisher ??= GetPublisher(entry);
        company ??= GetCompany(entry);

        Console.WriteLine("Product: " + product + ", " + package + ", " + publisher + ", " + company);

        // needed for IsMine function to work
        RegisterAssembly([
            Assembly.GetCallingAssembly(),
            ..Assembly.GetCallingAssembly().GetAssemblies(),
            Assembly.GetEntryAssembly(),
            ..Assembly.GetEntryAssembly()?.GetAssemblies() ?? [],
            Assembly.GetExecutingAssembly()]);

        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;

        TryLoadAllTypes();
    }

    private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        if (args.LoadedAssembly.IsMine())
        {
            RegisterAssembly(args.LoadedAssembly);

            TryLoadAllTypes();
        }
    }


    private static void TryLoadAllTypes()
    {

        try
        {
            MyRoutableInterfaces = [typeof(ILogin)];

            List<Type> components = [.._allMineTypes
            .Where(t => t.BaseType?.Name == "ComponentBase") // TODO: fix this for inherited components like accordion? no
                ];

            MyRoutable = [..components
            .Where(t => t.GetCustomAttributes().Any(StaticMatchRouteAttribute))
                ];

        }
        catch { }

        try
        {

            MyRoutes = [.._allMineTypes
            .SelectMany(TypeExtensions.Routes)
            .Distinct()
                ];
        } catch { }

        foreach (var type in MyRoutable)
        {
            // This triggers your existing GetRoutes logic to fill the _routeCache
            try
            {
                _ = GetRoutes(type);
            } catch { }
        }
    }


    public static void RegisterAssembly(params IEnumerable<Assembly?>? assemblies)
    {
        assemblies = [.. (assemblies ?? []).Concat(AppDomain.CurrentDomain.GetAssemblies())];

        if (assemblies == null) return;

        foreach (var assembly in assemblies ?? [])
        {
            if (assembly == null) continue;


            if (!_registeredAssemblies.Contains(assembly))
                _registeredAssemblies.Add(assembly);

            var name = assembly.FullName!;

            if (_loadedAssemblies.Contains(name)) continue;

            Console.WriteLine("Assembling: " + name);

            lock (_loaderLock)
            {
                if (_loadedAssemblies.Add(name))
                {
                    var mine = assembly.IsMine();
                    types[assembly] = assembly.GetAssTypesSafely();
                    foreach (var type in types[assembly])
                    {
                        _allKnownTypes.Add(type);
                        if (mine)
                        {
                            _allMineTypes.Add(type);
                        }
                    }
                }
            }
        }
    }

    private static readonly Dictionary<Assembly,List<Type>> types = [];


    public static List<Type> GetAssTypesSafely(this Assembly ass)
    {
        try
        {
            return types.TryGetValue(ass, out var assTypes) ? assTypes : types[ass] = [.. ass.GetTypes()];
        }
        catch (ReflectionTypeLoadException e)
        {
            // Return only the types that were successfully loaded
            return types.TryGetValue(ass, out var assTypes) ? assTypes : types[ass] = [.. e.Types.OfType<Type>()];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static readonly ConcurrentDictionary<string, Type?> _pathToTypeCache = new();

    private static readonly List<Assembly> _registeredAssemblies = [];
    public static List<Assembly> AllAssemblies { get => [.. _registeredAssemblies]; }
    private static readonly Assembly entry;
    public static readonly string? entryDirectory;
    private static readonly string? product;
    private static readonly string? package;
    private static readonly string? company;
    private static readonly string? publisher;

    public static List<Assembly> GetAssemblies(this Assembly assembly, params IEnumerable<Assembly>? calling)
    {
        RegisterAssembly([assembly, .. calling ?? []]);
        return [.. _registeredAssemblies];
    }

    public static List<Assembly> GetAssemblies(this AppDomain domain, params IEnumerable<Assembly>? calling)
    {
        RegisterAssembly([.. domain.GetAssemblies(), .. calling ?? []]);
        return [.. _registeredAssemblies];
    }


    public static List<Assembly> GetAssemblies(params IEnumerable<Assembly>? calling)
    {
        RegisterAssembly(calling);
        return [.. _registeredAssemblies];
    }



    public static List<Assembly> GetAssemblies(this IEnumerable<Assembly>? calling, params IEnumerable<Assembly>? calling2)
    {
        RegisterAssembly([..calling ?? [], ..calling2 ?? []]);
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
            var fileName = normalizedPath.Replace(".razor", "").Replace(".html", "").Replace(".cs", "");

            // 1. Direct Lookup (In case it's a fully qualified name string)
            var directType = Type.GetType(path) ?? Type.GetType(path.Split(',')[0]);
            if (directType != null) return directType;

            // 2. Exact Name Match (Fuzzy step 1)
            // Filters the master list for anything matching the file name (Component.razor -> Component)
            var potentialMatches = _allKnownTypes
                .Where(t => t.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase)
                    || t.FullName?.Equals(fileName, StringComparison.InvariantCultureIgnoreCase) == true
                    || t.Name.Equals(path.Split(',')[0], StringComparison.InvariantCultureIgnoreCase)
                    || t.FullName?.Equals(path.Split(',')[0], StringComparison.InvariantCultureIgnoreCase) == true
                    || t.AssemblyQualifiedName?.Equals(path, StringComparison.InvariantCultureIgnoreCase) == true)
                .ToList();

            Console.WriteLine("Trying to match against: " + _allKnownTypes.Count + " : " + fileName + " : " + JsonSerializer.Serialize(potentialMatches.Select(t => t.Name)));

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
