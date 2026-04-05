using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {

        static readonly ConcurrentDictionary<MemberInfo, List<Type>> parameterCache = [];

        public static List<Type> ToServices(this ParameterInfo[]? parameters, IServiceProvider collection)
        {
            if (parameters == null || parameters.Length == 0) return [];
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
            var type = typeof(T);
            return Route(type);
        }


        public static string? Route<T>(this Type? type) where T : class
        {
            if(type == null) return null;
            if (_cachedRouteTypes.TryGetValue(type, out var route)) return route;
            if (!type.HasAuthorization()) return null;
            var ns = (type.Namespace ?? "Global").Split('.').ToList();
            if (!string.IsNullOrWhiteSpace(type.Name))
                ns.Add(type.Name);
            if (ns.Count == 0) return null; // prevent global collisions
            string url = $"/api/{string.Join('/', ns)}";
            _cachedRouteTypes.TryAdd(type, url);
            return url;
        }


        public static string? Route(this MethodInfo? sharing)
        {
            if (sharing == null) return null;
            if (_cachedRouteMethods.TryGetValue(sharing, out var route)) return route;
            var type = sharing.DeclaringType;
            if (!sharing.HasAuthorization()) return null;
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
            return type.Route() != null;
        }


        public static bool IsRoutable(this MethodInfo sharing)
        {
            return sharing.Route() != null;
        }


        public static List<MethodInfo> Routes<T>(this T sharing) where T : class
            => [.. typeof(T).GetMethods(null).Where(m => m.IsRoutable())];


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
