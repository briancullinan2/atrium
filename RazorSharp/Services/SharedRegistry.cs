using Interfacing.Entity;
using Microsoft.EntityFrameworkCore;

namespace RazorSharp.Services;

public static class SharedRegistry
{
    public static List<Type> AllTranslations { get; }

    static SharedRegistry()
    {
        var assemblies = Assembly.GetCallingAssembly().GetAssemblies(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

        AllTranslations = [..assemblies
        .SelectMany(ass => ass.GetTypes())
            .Where(t => t.Extends(typeof(ITranslationContext)))
            ];

    }


    public static void BuildSharedServiceList(IServiceCollection Services)
    {
        Services.AddCascadingValue(sp => new ErrorBoundary());

        // LOL i was going to look at all "service" names and namespaces, then try to find other constructors they
        //   are used in, and if its at least one that should be a good list, automatically scoped unless it's routable?
        // FUCK DI

        foreach (var service in TypeExtensions.AllServices)
        {
            Services.AddScoped(service, service);
            if(service.BaseType != null && service.BaseType != typeof(object))
                Services.AddScoped(service.BaseType, service);
            foreach (var inter in service.GetInterfaces())
            {
                Services.AddScoped(inter, sp => sp.GetRequiredService(service));
            }
        }

        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();

        BuildSharedDatabases(Services);


        // TODO: this line is for testing
        //Services.AddSingleton<IQueryManager, RemoteManager>(sp => sp.GetRequiredService<RemoteManager>());
        // TODO: should be
        //Services.AddSingleton<IQueryManager, QueryManager>();
        //Services.AddSingleton<RemoteManager>();

    }


    public static void BuildSharedDatabases(IServiceCollection Services)
    {
        foreach (var translationType in AllTranslations)
        {
            // 1. Find the AddDbContextFactory method via reflection
            var method = typeof(EntityFrameworkServiceCollectionExtensions)
                .GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkServiceCollectionExtensions.AddDbContextFactory)
                       && m.GetParameters().Length == 2); // Looking for (IServiceCollection, Action<DbContextOptionsBuilder>)

            // 2. Turn AddDbContextFactory<T> into AddDbContextFactory<YourDynamicType>
            var genericMethod = method.MakeGenericMethod(translationType);

            // 3. Invoke it: Services.AddDbContextFactory<translationType>(options => ...)
            genericMethod.Invoke(null, new object?[] { Services, null });

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
