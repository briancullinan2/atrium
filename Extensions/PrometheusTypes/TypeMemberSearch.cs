
namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {


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
                        && (generic == null || (generic == 0 && !m.IsGenericMethod) || (m.GetGenericArguments().Length == generic)))
                    .ToList();

                var byParameter = method
                    .Where(m => extendedTypes == null
                        || m.GetParameters().Where((p, i) =>
                            extendedTypes.ElementAtOrDefault(i) is Type testTest
                            && p.ParameterType.Extends(testTest)).Count() == extendedTypes.Length)
                    .ToList();

                var ordered = byParameter.OrderBy(m =>
                            (m.GetParameters().Length == extendedTypes?.Length ? -1 : 0)
                            + m.GetParameters().Select((p, i) =>
                            p.ParameterType != typeof(object)
                            && p.ParameterType == extendedTypes?.ElementAtOrDefault(i)
                            ? -1 : 0).Sum()).ToList();


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

    }
}
