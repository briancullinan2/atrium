using Microsoft.AspNetCore.Authorization;
#if !BROWSER
using Microsoft.AspNetCore.Http;
#endif
using System.Collections.Concurrent;

namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {

        static readonly ConcurrentDictionary<MemberInfo, List<Type>> parameterCache = [];

        public static List<Type> ToServices(this IEnumerable<ParameterInfo>? parameters, IServiceProvider collection)
        {
            if (parameters == null || !parameters.Any()) return [];
            if (parameterCache.TryGetValue(parameters.First().Member, out var cached)) return cached;

            List<Type> services = [];
            foreach (ParameterInfo parameter in parameters)
            {
                if (parameter.ParameterType.IsPrimitive) continue;
                var exists = collection.GetService(parameter.ParameterType);
                if (exists == null) continue;
                services.Add(parameter.ParameterType);
            }
            return services;
        }


        public static string? Route<T>(this T _) where T : class
        {
            return Route(typeof(T));
        }


        public static string? Route<T>(this Type? type) where T : class
        {
            if(type == null) return null;
            if (_cachedRouteTypes.TryGetValue(type, out var route)) return route;
            if(type.GetCustomAttribute<RouteAttribute>() is RouteAttribute attr)
            {
                _cachedRouteTypes.TryAdd(type, attr.Template);
            }

            if (!type.HasAuthorization() && type.Routes().Count == 0) return null;

            var ns = (type.Namespace ?? "Global").Split('.').ToList();
            if (!string.IsNullOrWhiteSpace(type.Name))
                ns.Add(type.Name);
            if (ns.Count == 0) return null; // prevent global collisions
            string url = $"/api/{string.Join('/', ns)}";
            _cachedRouteTypes.TryAdd(type, url);
            return url;
        }


        public static bool TypeExtendsAny(this Type any, ParameterInfo type) => type.ParameterType.Extends(any);


        public static string? Route(this MethodInfo? sharing)
        {
            if (sharing == null) return null;
            if (_cachedRouteMethods.TryGetValue(sharing, out var route)) return route;
            var type = sharing.DeclaringType;
            if (!sharing.HasAuthorization()
#if !BROWSER
                && !sharing.GetParameters().Any(typeof(HttpContext).TypeExtendsAny)
#endif
            )
                return null;
            
            var ns = (type?.Namespace ?? "Global").Split('.').ToList();
            if (!string.IsNullOrWhiteSpace(type?.Name))
                ns.Add(type.Name);
            if (!string.IsNullOrWhiteSpace(sharing.Name))
                ns.Add(sharing.Name);
            if (ns.Count == 0) return null; // prevent global collisions
            string url = $"/api/{string.Join('/', ns)}";
            _cachedRouteMethods.TryAdd(sharing, url);
            return url;
        }



        public static bool IsRoutable<T>(this T _) where T : class
        {
            return typeof(T).Route() != null;
        }

        public static bool IsRoutable(this Type type)
        {
            return type.Route() != null || type.Routes().Count > 0;
        }


        public static bool IsRoutable(this MethodInfo sharing)
        {
            return sharing.Route() != null;
        }


        public static List<Type> ToServices(this IEnumerable<Assembly?>? ass)
        {
            if (ass == null) return [];
            return [..ass.SelectMany(a => a?.ToServices() ?? []).Where(t => t!=null)];
        }

        static readonly ConcurrentDictionary<Assembly, List<Type>> _serviceCache = [];

        public static List<Type> ToServices(this Assembly ass)
        {
            if (_serviceCache.TryGetValue(ass, out var services)) return services;
            List<Type> interfaces = [..ass.GetTypes().Where(t => t.Name.Contains("Service", StringComparison.InvariantCultureIgnoreCase)
                || t.Namespace?.Contains("Service", StringComparison.InvariantCultureIgnoreCase) == true)];
            List<Type> servicesToCache = [.. interfaces.Concat(ass.GetTypes().Where(interfaces.AnyExtendsAny))];
            _serviceCache.TryAdd(ass, servicesToCache);
            return servicesToCache;
        }


        public static bool AnyExtendsAny(
            this List<Type> interfaces,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
            Type any) => interfaces.Any(any.Extends);


        public static List<MethodInfo> Routes<T>(this T sharing) where T : class
            => [.. typeof(T).Routes()];

        public static List<MethodInfo> Routes(this Type sharing)
            => [.. sharing.GetMethods(null).Where(m => m.IsRoutable())];



        private static readonly ConcurrentDictionary<Type, string?> _cachedRouteTypes = [];
        private static readonly ConcurrentDictionary<MethodInfo, string?> _cachedRouteMethods = [];


        public static bool HasAuthorization<T>(this T _) where T : class
        {
            var type = typeof(T);
            return HasAuthorization(type);
        }

        public static bool HasAuthorization(this Type type)
        {
            if (_cachedRouteTypes.TryGetValue(type, out var route)) return route != null;
            bool hasAnonymous = type.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            bool hasAuthorize = type.GetCustomAttribute<AuthorizeAttribute>() != null;
            return hasAnonymous || hasAuthorize;

        }


        public static bool HasAuthorization(this MethodInfo sharing)
        {
            if (_cachedRouteMethods.TryGetValue(sharing, out var route)) return route != null;
            var type = sharing.DeclaringType;
            bool hasAnonymous = sharing.GetCustomAttribute<AllowAnonymousAttribute>() != null
                || type?.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            bool hasAuthorize = sharing.GetCustomAttribute<AuthorizeAttribute>() != null
                || type?.GetCustomAttribute<AuthorizeAttribute>() != null;
            return hasAnonymous || hasAuthorize;

        }

    }
}
