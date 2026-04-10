
using DataShared.Extensions;
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

        List<Type> AlreadyMapped = [];
        // TODO: need to map all IHasCurrent values to their functional Current static interface value
        //   do this before the service creator below reaches them
        var currents = AllServices.Where(s => s.Extends(typeof(IHasCurrent<>))).ToList();
        foreach (var cur in currents)
        {
            var currentType = cur.GenericTypeArguments[0];
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
