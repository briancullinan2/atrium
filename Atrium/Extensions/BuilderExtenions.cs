using Atrium.Services;
using Interfacing.Services;
using Microsoft.AspNetCore.Components.Web;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Atrium.Extensions;

internal static class BuilderExtensions
{
    internal static List<Type>? CachedAllServices;
    internal static List<Type> AllServices
    {
        get => CachedAllServices ??= [..AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(ass => ass.IsMine())
            .SelectMany(GetAssTypesSafely)
            .GetServicable()
            ];

    }


    public static List<Type> GetAssTypesSafely(this Assembly ass)
    {
        try
        {
            return [..ass.GetTypes()];
        }
        catch (ReflectionTypeLoadException e)
        {
            // Return only the types that were successfully loaded
            return [.. e.Types.OfType<Type>()];
        }
        catch (Exception)
        {
            return [];
        }
    }


    public static void BuildServices(this IServiceCollection Services, string? key = null)
    {
        BuildServices(Services, AllServices, key);
    }

    public static void BuildServices(this IServiceCollection Services, IEnumerable<Assembly> asses, string? key = null)
    {
        var currents = asses.SelectMany(GetAssTypesSafely).GetServicable().ToList();

        BuildServices(Services, currents, key);
    }


    public static void BuildServices(this IServiceCollection Services, List<Type> AllServices, string? key = null)
    {
        Services.AddCascadingValue(sp => new ErrorBoundary());

        List<Type> AlreadyMapped = [];
        // TODO: need to map all IHasCurrent values to their functional Current static interface value
        //   do this before the service creator below reaches them
        var currents = AllServices.Where(s => s.Extends(typeof(IHasCurrent<>))).ToList();
        foreach (var cur in currents)
        {
            var currentType = cur.GetInterfaces().First(i => i.Extends(typeof(IHasCurrent<>))).GenericTypeArguments[0];
            Services.AddCurrentAsLazy(cur, key);
            AlreadyMapped.Add(cur);
            AlreadyMapped.Add(currentType);
        }


        foreach (var service in AllServices)
        {
            if (AlreadyMapped.Contains(service))
                continue;

            Services.AddAutoServices(service, key);

        }

        var hasAuth = AllServices.Any(t => t.Extends(typeof(IAuthService)));


        // TODO: use IHasBuilder 
        //if (hasAuth)
        //{
        //    Services.AddAuthorizationCore();
        //    Services.AddCascadingAuthenticationState();
        //}

        //DatabaseBuilder.BuildServices(Services);


        // TODO: this line is for testing
        //Services.AddSingleton<IQueryManager, RemoteManager>(sp => sp.GetRequiredService<RemoteManager>());
        // TODO: should be
        //Services.AddSingleton<IQueryManager, QueryManager>();
        //Services.AddSingleton<RemoteManager>();

    }

    public static void AddLazyScoped(this IServiceCollection services, Type serviceType, Type implementationType, string? key = null)
    {
        var lazyType = typeof(Lazy<>).MakeGenericType(serviceType);
        if (key != null)
        {
            services.AddKeyedScoped(lazyType, key, (sp, key) =>
            {
                Func<object?> factory = () => sp.GetKeyedService(implementationType, key);
                return Activator.CreateInstance(lazyType, factory)!;
            });
        }
        else
        {
            services.AddScoped(lazyType, sp =>
            {
                Func<object?> factory = () => sp.GetService(implementationType);
                return Activator.CreateInstance(lazyType, factory)!;
            });
        }

    }

    // Standard Lazy wrapper for a service
    public static void AddLazyScoped(this IServiceCollection services, Type serviceType, string? key = null)
    {
        var lazyType = typeof(Lazy<>).MakeGenericType(serviceType);
        if (key != null)
        {
            services.AddKeyedScoped(lazyType, key, (sp, k) => {
                var factoryDelegateType = typeof(Func<>).MakeGenericType(serviceType);
                Func<object?> func = () => sp.GetRequiredKeyedService(serviceType, k);
                return Activator.CreateInstance(lazyType, func);
            });
        }
        else
        {
            services.AddScoped(lazyType, sp => {
                Func<object?> func = () => sp.GetRequiredService(serviceType);
                return Activator.CreateInstance(lazyType, func);
            });
        }
    }


    public static void AddAutoServices(this IServiceCollection Services, Type service, string? key = null)
    {
        if (key != null)
        {
            Services.AddKeyedScoped(service, key, service);
            if (service.BaseType != null && service.BaseType != typeof(object))
                Services.AddKeyedScoped(service.BaseType, key, service);
            foreach (var inter in service.GetInterfaces())
            {
                Services.AddKeyedScoped(inter, key, (sp, key) => sp.GetRequiredKeyedService(service, key));
            }
        }
        else
        {
            Services.AddScoped(service, service);
            if (service.BaseType != null && service.BaseType != typeof(object))
                Services.AddScoped(service.BaseType, service);
            foreach (var inter in service.GetInterfaces())
            {
                Services.AddScoped(inter, sp => sp.GetRequiredService(service));
            }
        }
    }


    // Maps IHasCurrent<T>.Current to a Lazy<T> in the DI container
    public static void AddCurrentAsLazy(this IServiceCollection services, Type typeImplementingHasCurrent, string? key = null)
    {
        // 1. Get T from IHasCurrent<T>
        var interfaceType = typeImplementingHasCurrent.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasCurrent<>));

        if (interfaceType == null) return;

        var tType = interfaceType.GetGenericArguments()[0];
        var lazyTType = typeof(Lazy<>).MakeGenericType(tType);

        // 2. Get the static property "Current"
        var prop = typeImplementingHasCurrent.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
        if (prop == null) return;

        // 3. Register the factory
        if (key != null)
        {
            services.AddKeyedScoped(lazyTType, key, (sp, _) => {
                var factoryDelegateType = typeof(Func<>).MakeGenericType(tType);
                var factory = Delegate.CreateDelegate(factoryDelegateType, null, prop.GetGetMethod()!);
                return Activator.CreateInstance(lazyTType, factory);
            });
        }
        else
        {
            services.AddScoped(lazyTType, sp => {
                var factoryDelegateType = typeof(Func<>).MakeGenericType(tType);
                var factory = Delegate.CreateDelegate(factoryDelegateType, null, prop.GetGetMethod()!);
                return Activator.CreateInstance(lazyTType, factory);
            });
        }
    }


    public static List<Type> GetServicable(this IEnumerable<Type> asses)
    {

        List<Type> plugins = [..asses
            .Where(t => t.IsConcrete() && t.Extends(typeof(IHasPlugins)))
            .SelectMany(t => t.GetProperty(nameof(IHasPlugins.Plugins), BindingFlags.Static)?.GetValue(null) as List<Type> ?? [])];

        asses = [.. asses.Concat(plugins)];

        List<Type> concrete = [.. asses.Where(s => s.IsConcrete())];

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
            .Where(t => t.Extends(typeof(IHasCurrent<>)))
            .Select(t => {
                var interf = t.GetInterfaces().First(i => i.Extends(typeof(IHasCurrent<>)));
                return interf.GetGenericArguments().First();
            })];

        return [.. servicable.Concat(currents).Distinct()];
    }


    public static bool Extends([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type? type, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type? genericDefinition)
    {
        if (type == null || genericDefinition == null) return false;

        // 1. Direct/Standard Check (Handles non-generics and exact matches)
        if (genericDefinition.IsAssignableFrom(type) || type.IsAssignableFrom(genericDefinition))
            return true;

        // 2. Generic Family Check
        // Get the "Open" version (e.g., Entity<Setting> -> Entity<>)
        var openDef = genericDefinition.IsGenericType
            ? genericDefinition.GetGenericTypeDefinition()
            : genericDefinition;

        var current = type;
        while (current != null && current != typeof(object))
        {
            // Check if the current type in hierarchy is the generic we're looking for
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openDef)
                return true;

            current = current.BaseType;
        }

        // 3. Check all implemented interfaces for a generic match
        if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openDef))
            return true;

        // 4. Reverse Check (In case we passed the open generic as the first argument)
        if (type.IsGenericType)
        {
            var openType = type.GetGenericTypeDefinition();
            if (genericDefinition.IsAssignableFrom(openType) ||
                genericDefinition.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openType))
                return true;
        }

        return false;
    }

    public static bool IsConcrete(this Type type)
    {
        if (type == null) return false;

        return !type.IsAbstract &&
               !type.IsInterface &&
               !type.IsGenericTypeDefinition;
    }

}