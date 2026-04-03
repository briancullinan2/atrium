using System.Collections.Concurrent;

namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {


        private static readonly ConcurrentBag<Type> _allKnownTypes = [];
        private static readonly HashSet<string> _loadedAssemblies = [];
        private static readonly Lock _loaderLock = new();


        static TypeExtensions()
        {
            RegisterAssembly(Assembly.GetExecutingAssembly());
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






        public static Type? ToType(this string filePath, Assembly? targetAssembly = null)
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
}
