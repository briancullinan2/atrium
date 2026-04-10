// TODO: THIS FEELS OUT OF PLACE
// SHOULD BE IN DATASHARED BUT DEPENDS ON EXTENSIONS
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.SlenderServices;

public class DatabaseBuilder : IHasBuilder
{
    public static List<Type> AllTranslations { get; }

    static DatabaseBuilder()
    {

        AllTranslations = [..TypeExtensions.AllRegisteredTypes
            .Except([typeof(object)])
            .Where(typeof(ITranslationContext).Extends)
        ];

    }

    public static void BuildServices(IServiceCollection Services, string? key = null)
    {
        var concrete = AllTranslations.Where(t => t.IsConcrete()).ToList();
        foreach (var translationType in concrete)
        {
            // 1. Find the AddDbContextFactory method via reflection
            var method = typeof(EntityFrameworkServiceCollectionExtensions)
                .GetMethod(nameof(EntityFrameworkServiceCollectionExtensions.AddDbContextFactory), BindingFlags.Static)
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
}