
using Atrium.Services;

namespace Atrium.Extensions;

public static class BuilderExtensions
{
    
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


    public static void BuildServices(this IServiceCollection Services, List<Type> AllServices, string? key = null, IServiceProviderIsService? alreadyMapped = null, bool isSingleUser = false)
    {
        //alreadyMapped ??= [];
        // TODO: need to map all IHasCurrent values to their functional Current static interface value
        //   do this before the service creator below reaches them

        /*

        var currents = AllServices.Where(s => s.Extends(typeof(IHasCurrent<>))).ToList();
        foreach (var cur in currents)
        {
            if (cur.Extends(typeof(IHasNoService))) continue;
            if (alreadyMapped?.IsService(cur) == true 
                && Services.Any(s => s.ServiceType == cur)) continue;
            var currentType = cur.GetInterfaces().First(i => i.Extends(typeof(IHasCurrent<>))).GetGenericArguments()[0];
            if (alreadyMapped?.IsService(currentType) == true
                && Services.Any(s => s.ServiceType == currentType)) continue;
            Services.AddCurrentAsLazy(cur, key);
        }

        */

        foreach (var service in AllServices)
        {
            try
            {

                if (service.Extends(typeof(IHasNoService))) continue;


                var interfaces = service.GetInterfaces();
                //Console.WriteLine("Concrete: " + service.Name + " - " + JsonSerializer.Serialize(interfaces.Select(i => i.Name)));
                var currentType = interfaces.FirstOrDefault(i => i.Extends(typeof(IHasCurrent<>)));
                var iHasService = interfaces.FirstOrDefault(i => i.Extends(typeof(IHasService)));
                var iHasSingleton = interfaces.FirstOrDefault(i => i.Extends(typeof(ISingleton)));
                var iHasSingleUser = interfaces.FirstOrDefault(i => i.Extends(typeof(ISingleUser)));

                //var alreadyMapped = AlreadyMapped.Contains(service);
                if (service == typeof(TrustedLoader))
                {
                    Console.WriteLine("here");
                }
                if (iHasSingleton != null)
                {
                    Console.WriteLine("singleton?");
                }
                if (iHasSingleUser != null)
                {
                    Console.WriteLine("single user? " + isSingleUser);
                }
                // IHasCurrent<Application> the container is also automagically a singleton, for IHasCurrent<WebServer> to work too
                // static Current {get;} are inherently singletons
                // IHasService service containers are inherently singletons
                if (currentType != null || iHasService != null || iHasSingleton != null
                    || (iHasSingleUser != null && isSingleUser))
                {
                    Services.AddAutoSingleton(service, key, alreadyMapped);

                }
                else
                {
                    Services.AddAutoScoped(service, key, alreadyMapped);

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

        //var hasAuth = AllServices.Any(t => t.Extends(typeof(IAuthService)));


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


    public static void AddAutoScoped(this IServiceCollection Services, Type service, string? key = null, IServiceProviderIsService? baseAlreadyMapped = null)
    {
        var types = Services.Select(s => s.ServiceType).ToArray();
        var interfaces = service.GetInterfaces();
        if (interfaces.Length == 1 && interfaces.First().Extends(typeof(IHasCurrent<>)))
            return;

        if (key != null)
        {
            if (baseAlreadyMapped?.IsService(service) != true && !types.Contains(service))
                Services.AddKeyedScoped(service, key);
            if (service.BaseType != null && baseAlreadyMapped?.IsService(service.BaseType) != true 
                && service.BaseType != typeof(object)
                && !types.Contains(service.BaseType))
                Services.AddKeyedScoped(service.BaseType, key, (sp, key) => sp.GetRequiredKeyedService(service, key));
            foreach (var inter in interfaces)
            {
                if (types.Contains(inter)) continue;
                if (baseAlreadyMapped?.IsService(inter) == true) continue;
                Services.AddKeyedScoped(inter, key, (sp, key) => sp.GetRequiredKeyedService(service, key));
            }
        }
        else
        {
            if (baseAlreadyMapped?.IsService(service) != true && !types.Contains(service))
                Services.AddScoped(service);
            if (service.BaseType != null && baseAlreadyMapped?.IsService(service.BaseType) != true 
                && service.BaseType != typeof(object)
                && !types.Contains(service.BaseType))
                Services.AddScoped(service.BaseType, sp => sp.GetRequiredService(service));
            foreach (var inter in interfaces)
            {
                if (types.Contains(inter)) continue;
                if (baseAlreadyMapped?.IsService(inter) == true) continue;
                Services.AddScoped(inter, sp => sp.GetRequiredService(service));
            }
        }
    }


    public static void AddAutoSingleton(this IServiceCollection Services, Type service, string? key = null, IServiceProviderIsService? baseAlreadyMapped = null)
    {
        var types = Services.Select(s =>  s.ServiceType).ToArray();
        var interfaces = service.GetInterfaces();
        if (interfaces.Length == 1 && interfaces.First().Extends(typeof(IHasCurrent<>)))
            return;

        if (key != null)
        {
            if(baseAlreadyMapped?.IsService(service) != true && !types.Contains(service))
                Services.AddKeyedSingleton(service, (object?)key);
            if (service.BaseType != null && baseAlreadyMapped?.IsService(service.BaseType) != true 
                && service.BaseType != typeof(object)
                && !types.Contains(service.BaseType))
                Services.AddKeyedSingleton(service.BaseType, key, (sp, key) => sp.GetRequiredKeyedService(service, key));
            foreach (var inter in interfaces)
            {
                if (types.Contains(inter)) continue;
                if (baseAlreadyMapped?.IsService(inter) == true) continue;
                Services.AddKeyedSingleton(inter, key, (sp, key) => sp.GetRequiredKeyedService(service, key));
            }
        }
        else
        {
            if (baseAlreadyMapped?.IsService(service) != true && !types.Contains(service))
                Services.AddSingleton(service);
            if (service.BaseType != null && baseAlreadyMapped?.IsService(service.BaseType) != true 
                && service.BaseType != typeof(object)
                && !types.Contains(service.BaseType))
                Services.AddSingleton(service.BaseType, sp => sp.GetRequiredService(service));
            foreach (var inter in interfaces)
            {
                if (types.Contains(inter)) continue;
                if (baseAlreadyMapped?.IsService(inter) == true) continue;
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