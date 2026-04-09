
using Microsoft.EntityFrameworkCore;

namespace RazorSharp.Services;

public static class SharedRegistry
{
    public static List<Type> AllTranslations { get; }
    public static List<Type> AllServices { get; private set; }

    static SharedRegistry()
    {
        TypeExtensions.RegisterAssembly(Assembly.GetCallingAssembly());

        AllTranslations = [..TypeExtensions.AllRegisteredTypes
            .Except([typeof(object)])
            .Where(typeof(ITranslationContext).Extends)
        ];

        // LOL i was going to look at all "service" names and namespaces, then try to find other constructors they
        //   are used in, and if its at least one that should be a good list, automatically scoped unless it's routable?
        // FUCK DI
        AllServices = [..TypeExtensions.AllRegisteredTypes
            .GetMine()
            .GetServicable()];

    }


    public static void BuildSharedServiceList(IServiceCollection Services, string? key = null)
    {
        Services.AddCascadingValue(sp => new ErrorBoundary());

        foreach (var service in AllServices)
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

        var hasAuth = AllServices.Any(t => t.Extends(typeof(IAuthService)));

        if (hasAuth)
        {
            Services.AddAuthorizationCore();
            Services.AddCascadingAuthenticationState();
        }

        BuildSharedDatabases(Services);


        // TODO: this line is for testing
        //Services.AddSingleton<IQueryManager, RemoteManager>(sp => sp.GetRequiredService<RemoteManager>());
        // TODO: should be
        //Services.AddSingleton<IQueryManager, QueryManager>();
        //Services.AddSingleton<RemoteManager>();

    }


    public static void BuildSharedDatabases(IServiceCollection Services)
    {
        var concrete = AllTranslations.Where(t => t.IsConcrete()).ToList();
        foreach (var translationType in concrete)
        {
            // 1. Find the AddDbContextFactory method via reflection
            var method = typeof(EntityFrameworkServiceCollectionExtensions)
                .GetMethods(nameof(EntityFrameworkServiceCollectionExtensions.AddDbContextFactory), 1)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Could not render AddDbContextFactory method");

            // 2. Turn AddDbContextFactory<T> into AddDbContextFactory<YourDynamicType>
            var genericMethod = method.MakeGenericMethod(translationType);

            // 3. Invoke it: Services.AddDbContextFactory<translationType>(options => ...)
            genericMethod.Invoke(null, [ Services, null, ServiceLifetime.Singleton ]);

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
}
