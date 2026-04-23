

using Atrium.Components;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace Atrium.Services;



public class PluginActivator(ICompositeProvider Composite, IServiceProvider Service) : IComponentActivator //, ISingleUser //, IHasCurrent<PluginActivator> // Current is null
{
    private static readonly FieldInfo? renderMode;

    static PluginActivator()
    {
        renderMode = typeof(ComponentBase).GetField("_renderMode", BindingFlags.Instance | BindingFlags.NonPublic);

    }

    // TODO: replace Presentation with an extended type selected by main layout or query string

    public IComponent CreateInstance(Type componentType)
    {
        /*if (componentType.GetInterfaces().Any(inter => inter == typeof(IHasCurrent<RenderFragment>)) {
            var frag = (GetType().GetProperty("Current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as RenderFragment)
            return componentType.
        }*/

        IComponent? serviceComponent = null;
        //var scoped = Composite.CreateScope().ServiceProvider;
        if(Service.GetService<IServiceProviderIsService>()?.IsService(componentType) == true)
            serviceComponent = (IComponent)Service.GetRequiredService(componentType);

        else if (Composite.IsService(componentType))
            serviceComponent = (IComponent)Composite.GetRequiredService(componentType);

        else
            serviceComponent = (IComponent)ActivatorUtilities.CreateInstance(Composite, componentType);

        if (serviceComponent is ComponentBase baseComponent
                && Composite.GetService<IFormFactor>()?.IsWebContext == true)
        {
            renderMode?.SetValue(baseComponent, new ValueTuple<IComponentRenderMode?, bool>(InteractiveServer, true));
        }

        serviceComponent.InjectService(Composite);
        // TODO: IHasCurrent, always use Current IComponent instead of creating a new one

        return serviceComponent;
    }

}



public partial class CompositeServiceProvider(IServiceProvider _provider)
    : IServiceProvider
    , ISupportRequiredService
    , IHasService
    , IServiceScopeFactory
    , IServiceProviderIsService
    , ICompositeProvider
    , IServiceScope
{

    public static List<Type> BuiltIn { get; } = [
        typeof(PluginActivator),
        typeof(CompositeServiceProvider),
        typeof(RenderStateProvider),
        typeof(Atrium.Components.MainLoader)
    ];


    public List<Type> SingleUser { get; } = [
        typeof(HttpClient),
        typeof(NavigationManager),
        typeof(IJSRuntime),
        typeof(IConfiguration),
        //{typeof(ILogger<>), typeof(Logger<>)  },
        typeof(ILoggerFactory),
#if !BROWSER
        typeof(Lazy<Atrium.Components.MainLoader?>),
        typeof(Lazy<Application?>),
        typeof(Microsoft.Extensions.Hosting.IHostEnvironment),
#endif
    ];


    public List<Type> UserTypes { get; } = [];

    public IServiceProvider Services => this;
    // something you got to introduce a little... anarchy
    public List<IServiceProvider> PluginContainers { get; } = [_provider];

    public object GetService(Type serviceType)
    {
        if (serviceType == typeof(CompositeServiceProvider))
            return this;
        if (serviceType == typeof(ICompositeProvider))
            return this;
        //if (serviceType == typeof(IServiceProvider))
        //    return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return this;
        if (serviceType == typeof(IServiceScopeFactory))
            return this;


        try
        {
            List<IServiceProvider>? MyPlugins = null;
            lock (PluginContainers) MyPlugins = [..PluginContainers];
            foreach(var container in MyPlugins ?? [])
            {
                var isService = container.GetService<IServiceProviderIsService>();
                if (isService?.IsService(serviceType) == true)
                {
                    return container.GetRequiredService(serviceType);
                }
            }
            if (PluginContainers.Count <= 1)
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!Plugins is null!!!!!!!!!!!!!!");

            if(serviceType == typeof(PluginsPage))
            {
                Console.WriteLine("Plugins created");
            }

            var serviceComponent = CreateService(serviceType)!;
            serviceComponent.InjectService(this);
            return serviceComponent;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }

    }

    static Dictionary<Type, List<Type>>? services;

    static CompositeServiceProvider()
    {
        lock (TrustedLoader.StoredServiceable)
            services = TrustedLoader.StoredServiceable.ToDictionary();
    }



    public object? CreateService(Type serviceType)
    {
        var collection = new ServiceCollection();

        lock(TrustedLoader.StoredServiceable)
            services = TrustedLoader.StoredServiceable.ToDictionary();

        var baseType = services.FirstOrDefault(kvp => kvp.Key.Extends(serviceType)).Key
            ?? services.FirstOrDefault(kvp => kvp.Value.Any(inter => inter == serviceType)).Key;

        if (baseType == null) return null;

        var constructors = baseType.GetConstructors();

        var moreRequirements = constructors
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        var moreServices = services
            .Where(kvp => moreRequirements.Any(req => kvp.Key.Extends(req)
                || kvp.Value.Any(inter => inter.Extends(req))))
            .Select(kvp => kvp.Key)
            .ToList();

        List<Type> existingTypes = [.. BuiltIn.Concat(SingleUser).Concat(UserTypes).Distinct()];
        AddExisting(this, collection, existingTypes);
        collection.BuildServices([baseType, .. moreServices, .. existingTypes], null, this, true);

        foreach (var t in collection) 
            if (!existingTypes.Contains(t.ServiceType)) 
                UserTypes.Add(t.ServiceType);


        var Services = collection.BuildServiceProvider();
        lock(PluginContainers)
            PluginContainers.Add(Services);
        var service2 = Services.GetService(serviceType);
        
        return service2;
    }



    protected static void AddExisting(IServiceProvider Provider, IServiceCollection collection, List<Type> checkExisting)
    {
        var isService = Provider.GetService<IServiceProviderIsService>();
        var types = checkExisting.Concat(checkExisting.SelectMany(e => e.GetInterfaces()));
        foreach (var ass in types)
        {
            try
            {
                if (isService?.IsService(ass) == true)
                {
                    if (ass.Extends(typeof(IHasService)))
                        collection.AddSingleton(ass, sp => Provider.GetRequiredService(ass));
                    else
                        collection.AddScoped(ass, sp => Provider.GetRequiredService(ass));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Who the fuck even are you? " + ex);
            }

        }
    }


    public object GetRequiredService(Type serviceType)
    {
        var service = GetService(serviceType)
            ?? throw new InvalidOperationException($"Service {serviceType} not found in either container.");
        return service;
    }

    public IServiceProvider ServiceProvider => this;

    public IServiceScope CreateScope()
    {
        var scoped = new CompositeServiceProvider(_provider.CreateScope().ServiceProvider);
        scoped.PluginContainers.Add(this);
        return scoped;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public bool IsService(Type serviceType)
    {
        //if (services?.ContainsKey(serviceType) == true
        //    || services?.Any(s => s.Value.Contains(serviceType)) == true)
        //    return true;
        foreach (var container in PluginContainers)
        {
            if (container.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true)
            {
                return true;
            }
        }
        return false;
    }
}

internal static class InjectionExtensions
{
    public static void InjectService(this object? serviceComponent, IServiceProvider? Composite)
    {
        var componentType = serviceComponent?.GetType();
        var properties = componentType?
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() != null);

        foreach (var prop in properties ?? [])
        {

            var service = Composite?.GetService(prop.PropertyType);
            if (service != null)
            {
                prop.SetValue(serviceComponent, service);
            }

        }

    }
}
