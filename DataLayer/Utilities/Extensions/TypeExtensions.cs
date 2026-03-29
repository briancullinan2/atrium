using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace DataLayer.Utilities.Extensions
{
    // Token: 0x0200005E RID: 94
    public static class TypeExtensions
    {
        // Token: 0x060002F6 RID: 758 RVA: 0x00019200 File Offset: 0x00017400
        public static bool IsLocked(this object o)
        {
            bool result;
            if (!Monitor.TryEnter(o))
            {
                result = true;
            }
            else
            {
                Monitor.Exit(o);
                result = false;
            }
            return result;
        }

        // Token: 0x060002F7 RID: 759 RVA: 0x00019228 File Offset: 0x00017428
        public static IEnumerable<Type> GetBaseTypes(this Type type)
        {
            IEnumerable<Type> result;
            if (type.BaseType != null)
            {
                result = new Type[]
                {
                type.BaseType
                }.Concat(type.BaseType.GetBaseTypes());
            }
            else
            {
                result = [];
            }
            return result;
        }

        // Token: 0x060002F8 RID: 760 RVA: 0x00019278 File Offset: 0x00017478
        public static Type? GenericExtendsType(this Type type, Type ofType)
        {
            foreach (Type type2 in type.GetBaseTypes())
            {
                if (type2.IsGenericType)
                {
                    if (type2.GetGenericTypeDefinition() == ofType)
                    {
                        return type2;
                    }
                }
            }
            return null;
        }


        private static readonly Type[] IterableBaseTypes =
        [
            typeof(IEnumerable<>),
            typeof(IAsyncEnumerable<>),
            typeof(IEnumerable) // Catch-all for non-generic collections like ArrayList
        ];

        public static bool IsIterable(this Type type)
        {
            // 1. Strings are technically char sequences, but we treat them as atoms
            if (type == typeof(string)) return false;

            // 2. Arrays are always iterable
            if (type.IsArray) return true;

            // 3. Check the Type itself if it's an open or closed generic of our bases
            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (IterableBaseTypes.Any(baseType => baseType == def)) return true;
            }

            // 4. Check the interface hierarchy
            return type.GetInterfaces().Any(i =>
                i == typeof(IEnumerable) ||
                (i.IsGenericType && IterableBaseTypes.Contains(i.GetGenericTypeDefinition()))
            );
        }



        private static readonly NullabilityInfoContext context = new();


        public static bool IsNullable(this MethodInfo method)
        {
            var context = new NullabilityInfoContext();

            // 1. Check the Return Type (e.g., public string? GetName())
            var returnInfo = context.Create(method.ReturnParameter);
            if (returnInfo.ReadState == NullabilityState.Nullable)
            {
                return true;
            }
            return false;
        }


        public static bool IsNullable(this ParameterInfo parameter)
        {
            var context = new NullabilityInfoContext();

            var paramInfo = context.Create(parameter);
            if (paramInfo.WriteState == NullabilityState.Nullable)
            {
                return true;
            }
            return false;
        }


        public static bool IsNullable(this Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }


        public static bool IsNullable(this PropertyInfo property)
        {
            var info = context.Create(property);

            if (info.WriteState == NullabilityState.Nullable ||
                info.ReadState == NullabilityState.Nullable)
            {
                return true;
            }
            return false;
        }


        public static bool ReadOnly(this PropertyInfo property)
        {
            return !property.IsWritable();
        }


        public static bool IsWritable(this PropertyInfo property)
        {
            return property.CanWrite && property.GetSetMethod(nonPublic: true) != null;
        }



        private static readonly Dictionary<Type, EntityMetadata> _metadataCache = [];




        [Obsolete("This probably isn't what you want, Metadata of a Metadata?")]
        public static EntityMetadata Metadata(this EntityMetadata any)
        {
            return any;
        }


        public static EntityMetadata Metadata(this object any)
        {
            return any.GetType().Metadata();
        }

        public static EntityMetadata Metadata(this Type any)
        {
            if (_metadataCache.TryGetValue(any, out var meta))
                return meta;
            var newMeta = new EntityMetadata(any);
            _metadataCache.TryAdd(any, newMeta);
            return newMeta;
        }


        public static IEnumerable<PropertyInfo> GetProperties(this Type? type, string? name = null, int? generic = null, Type[]? extendedTypes = null, bool all = false)
        {
            var results = new List<PropertyInfo>();
            while (type != null)
            {

                var method = type
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(m => (name == null || m.Name == name)
                    && (generic == null || (generic == 0 && !m.PropertyType.IsGenericType) || (m.PropertyType.GetGenericArguments().Length == generic))
                    && (extendedTypes == null
                        || m.GetIndexParameters().Where((p, i) =>
                            extendedTypes.ElementAtOrDefault(i) is Type testTest
                            && p.ParameterType.Extends(testTest)).Count() == extendedTypes.Length)
                    );
                results = [.. results, .. method];
                type = type.BaseType;
                if (results.Count > 0 && !all) break;
            }
            return [.. results];
        }

        public static IEnumerable<MethodInfo> GetMethods(this Type? type, string? name = null, int? generic = null, Type[]? extendedTypes = null, bool all = false)
        {
            var results = new List<MethodInfo>();
            while (type != null)
            {


                var method = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(m => (name == null || m.Name == name)
                    && (generic == null || (generic == 0 && !m.IsGenericMethod) || (m.GetGenericArguments().Length == generic))
                    && (extendedTypes == null
                        || m.GetParameters().Where((p, i) =>
                            extendedTypes.ElementAtOrDefault(i) is Type testTest
                            && p.ParameterType.Extends(testTest)).Count() == extendedTypes.Length)
                    )
                .OrderBy(m => m.GetParameters().Select((p, i) =>
                            p.ParameterType != typeof(object) ? -1 : 0).Sum());
                var ordered = method;
                if(extendedTypes != null)
                    ordered = method
                        .OrderBy(m => m.GetParameters().Select((p, i) => 
                            p.ParameterType == extendedTypes.ElementAtOrDefault(i) ? -1 : 0).Sum());

                results = [.. results, .. ordered];
                type = type.BaseType;
                if (results.Count > 0 && !all) break;
            }
            return [.. results];
        }


        public static IEnumerable<FieldInfo> GetFields(this Type? type, string? name = null, int? generic = null, bool all = false)
        {
            var results = new List<FieldInfo>();
            var safety = 10;
            while (type != null)
            {

                var method = type
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(m => (name == null || m.Name == name)
                        && (generic == null || (generic == 0 && !m.FieldType.IsGenericType) || (m.FieldType.GetGenericArguments().Length == generic))
                        )
                    .ToList();
                results.AddRange(method);
                type = type.BaseType;
                if (--safety == 0) throw new InvalidOperationException("Whats going on?");
                if (results.Count > 0 && !all) break;
            }
            return results;
        }


        public static bool Extends(this Type type, Type? genericDefinition)
        {
            if (genericDefinition == null) return false;
            return genericDefinition.IsAssignableFrom(type) || IsCompatibleWith(type, genericDefinition)
                || type.IsAssignableFrom(genericDefinition) || IsCompatibleWith(genericDefinition, type);
        }


        public static bool IsCompatibleWith(this Type type, Type genericDefinition)
        {
            // 1. Is the type itself the generic definition (e.g., IEnumerable<T> == IEnumerable<>)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
                return true;

            if (genericDefinition.IsGenericType && genericDefinition.GetGenericTypeDefinition() == type)
                return true;

            // 2. Does it implement the interface (e.g., List<T> implements IEnumerable<T>)
            return type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == genericDefinition)
                || genericDefinition.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == type);
        }

        // Token: 0x060002F9 RID: 761 RVA: 0x000192F8 File Offset: 0x000174F8
        public static Type? GenericImplementsType(this Type type, Type ofType)
        {
            foreach (Type type2 in type.GetInterfaces())
            {
                if (type2.IsGenericType)
                {
                    if (type2.GetGenericTypeDefinition() == ofType)
                    {
                        return type2;
                    }
                }
            }
            return null;
        }

        public static string Limit(this string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0)
                return string.Empty;

            if (value.Length <= maxLength)
                return value;

            // If the field is too small for "...", just hard truncate
            if (maxLength <= 3)
                return value[..maxLength];

            // Standard truncation with suffix
            return value[..(Math.Min(value.Length, maxLength) - 3)] + "...";
        }

        public static bool IsSimple(this Type anyType)
        {
            var type = Nullable.GetUnderlyingType(anyType) ?? anyType;
            return type.IsPrimitive ||
                type.IsEnum ||
                new[] {
            typeof(string),
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid)
                }.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object ||
                (type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(Nullable<>)
                    && type.GetGenericArguments().FirstOrDefault()?.IsSimple() == true);
        }


        private static readonly ConcurrentBag<Type> _allKnownTypes = [];
        private static readonly HashSet<string> _loadedAssemblies = [];
        private static readonly Lock _loaderLock = new();


        static TypeExtensions()
        {
            RegisterAssembly(Assembly.GetExecutingAssembly());
        }


        public static void RegisterAssembly(Assembly? assembly)
        {
            if (assembly == null) return;

            var name = assembly.FullName!;
            if (_loadedAssemblies.Contains(name)) return;

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

        private static readonly ConcurrentDictionary<string, Type?> _pathToTypeCache = new();



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
