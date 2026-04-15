// TODO: THIS FEELS OUT OF PLACE
// SHOULD BE IN DATASHARED BUT DEPENDS ON EXTENSIONS
using Interfacing.Services;

namespace DataShared.Extensions;

public class DatabaseBuilder : IHasBuilder
{
    public static List<Type> AllTranslations { get; }

    static DatabaseBuilder()
    {
        var allKnownTypes = Assembly.GetCallingAssembly().GetTypes();
        AllTranslations = [..allKnownTypes
            .Except([typeof(object)])
            .Where(typeof(ITranslationContext).Extends)
        ];

    }

    public static void BuildServices(IServiceCollection Services, List<Type> AllServices, string? key = null)
    {

        List<Type> AlreadyMapped = [];
        // TODO: need to map all IHasCurrent values to their functional Current static interface value
        //   do this before the service creator below reaches them
        var currents = AllServices.Where(s => s.Extends(typeof(IHasCurrent<>))).ToList();
        foreach (var cur in currents)
        {
            if (cur.Extends(typeof(IHasNoService))) continue;
            var currentType = cur.GetInterfaces().First(i => i.Extends(typeof(IHasCurrent<>))).GetGenericArguments()[0];
            Services.AddCurrentAsLazy(cur, key);
            if (cur.GetInterfaces().Length == 1)
                AlreadyMapped.Add(cur);
            AlreadyMapped.Add(currentType); // force lazy usage for service type
        }


        foreach (var service in AllServices)
        {
            if (service.Extends(typeof(IHasNoService))) continue;

            var currentType = service.GetInterfaces().FirstOrDefault(i => i.Extends(typeof(IHasCurrent<>)));
            var iHasService = service.GetInterfaces().FirstOrDefault(i => i.Extends(typeof(IHasService)));
            var iHasSingleton = service.GetInterfaces().FirstOrDefault(i => i.Extends(typeof(ISingleton)));
            var iHasSingleUser = service.GetInterfaces().FirstOrDefault(i => i.Extends(typeof(ISingleUser)));

            // IHasCurrent<Application> the container is also automagically a singleton, for IHasCurrent<WebServer> to work too
            if (currentType != null || iHasService != null || iHasSingleton != null)
            // ISingleUser we know were in web server mode because its not using the Trust builder
            {
                Services.AddAutoSingleton(service, key);

            }
            else
            {
                Services.AddAutoScoped(service, key);

            }

        }

    }


    public static void BuildServices(IServiceCollection Services, string? key = null)
    {
        var concrete = AllTranslations.Where(t => t.IsConcrete()).ToList();
        foreach (var translationType in concrete)
        {
            // 1. Find the AddDbContextFactory method via reflection
            var method = typeof(EntityFrameworkServiceCollectionExtensions)
                .GetMethod(nameof(EntityFrameworkServiceCollectionExtensions.AddDbContextFactory), BindingFlags.Static | BindingFlags.Public)
                ?? throw new InvalidOperationException("Could not render AddDbContextFactory method");

            // 2. Turn AddDbContextFactory<T> into AddDbContextFactory<YourDynamicType>
            var genericMethod = method.MakeGenericMethod(translationType);

            // 3. Invoke it: Services.AddDbContextFactory<translationType>(options => ...)
            genericMethod.Invoke(null, [Services, null, ServiceLifetime.Singleton]);

            // 4. Register the Scoped DbContext using the Factory
            // We need to construct the IDbContextFactory<T> type dynamically
            var factoryType = typeof(IDbContextFactory<>).MakeGenericType(translationType);

            Services.AddScoped(translationType, sp =>
            {
                var factory = sp.GetRequiredService(factoryType);
                // Invoke CreateDbContext() via dynamic or reflection
                return ((dynamic)factory).CreateDbContext();
            });
        }
    }
}


public static class BuilderExtensions
{

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


    public static void AddAutoScoped(this IServiceCollection Services, Type service, string? key = null)
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


    public static void AddAutoSingleton(this IServiceCollection Services, Type service, string? key = null)
    {
        if (key != null)
        {
            Services.AddKeyedSingleton(service, key, service);
            if (service.BaseType != null && service.BaseType != typeof(object))
                Services.AddKeyedSingleton(service.BaseType, key, service);
            foreach (var inter in service.GetInterfaces())
            {
                Services.AddKeyedSingleton(inter, key, (sp, key) => sp.GetRequiredKeyedService(service, key));
            }
        }
        else
        {
            Services.AddSingleton(service, service);
            if (service.BaseType != null && service.BaseType != typeof(object))
                Services.AddSingleton(service.BaseType, service);
            foreach (var inter in service.GetInterfaces())
            {
                Services.AddSingleton(inter, sp => sp.GetRequiredService(service));
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
            var factoryDelegateType = typeof(Func<>).MakeGenericType(tType);
            var factory = Delegate.CreateDelegate(factoryDelegateType, null, prop.GetGetMethod()!);
            services.AddKeyedSingleton(lazyTType, key, (sp, _) => {
                return Activator.CreateInstance(lazyTType, factory);
            });
            services.AddKeyedSingleton(tType, key, (sp, _) => factory.DynamicInvoke());
        }
        else
        {
            var factoryDelegateType = typeof(Func<>).MakeGenericType(tType);
            var factory = Delegate.CreateDelegate(factoryDelegateType, null, prop.GetGetMethod()!);
            services.AddSingleton(lazyTType, sp => {
                return Activator.CreateInstance(lazyTType, factory);
            });
            services.AddSingleton(tType, sp => factory.DynamicInvoke());
        }
    }
}

