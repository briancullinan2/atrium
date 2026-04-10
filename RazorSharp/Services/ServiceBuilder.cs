
using Microsoft.EntityFrameworkCore;

namespace RazorSharp.Services;

public class ServiceBuilder : IHasBuilder
{
    public static List<Type> AllServices { get; private set; }

    static ServiceBuilder()
    {
        TypeExtensions.RegisterAssembly(Assembly.GetCallingAssembly());

        // LOL i was going to look at all "service" names and namespaces, then try to find other constructors they
        //   are used in, and if its at least one that should be a good list, automatically scoped unless it's routable?
        // FUCK DI
        AllServices = [..TypeExtensions.AllRegisteredTypes
            .GetMine()
            .GetServicable()];

    }


    public static void BuildServices(IServiceCollection Services, string? key = null)
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

        DatabaseBuilder.BuildServices(Services);


        // TODO: this line is for testing
        //Services.AddSingleton<IQueryManager, RemoteManager>(sp => sp.GetRequiredService<RemoteManager>());
        // TODO: should be
        //Services.AddSingleton<IQueryManager, QueryManager>();
        //Services.AddSingleton<RemoteManager>();

    }



}
