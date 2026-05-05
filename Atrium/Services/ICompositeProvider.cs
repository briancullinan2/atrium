

using Atrium.Components;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace Atrium.Services;


// step #1. fuck microsoft di

public partial class CompositeServiceProvider(IServiceCollection _provider, bool _scoped = false) : List<ServiceDescriptor>
    , IServiceProvider
    , ISupportRequiredService
    , IHasService
    , IServiceScopeFactory
    , IServiceProviderIsService
    , ICompositeProvider
    , IServiceScope
    , IServiceCollection
    , IDisposable
{


    public new IEnumerator<ServiceDescriptor> GetEnumerator()
    {
        // Return a concatenated stream of all descriptors in all plugin containers
        return PluginContainers
            .Concat(DisposableContainers)
            .SelectMany(container => container)
            .GetEnumerator();
    }


    public new void Add(ServiceDescriptor item) => throw new InvalidOperationException("Use PluginContainers to add services.");
    public new void Clear() => throw new InvalidOperationException("Clear individual plugin containers instead.");

    public List<Type> UserTypes { get; } = [];
    public Dictionary<Type, object> Disposables { get; } = [];
    public Dictionary<Type, object> Indisposables { get; } = [];

    public IServiceProvider Services => this;
    // something you got to introduce a little... anarchy
    public List<IServiceCollection> PluginContainers { get; } = [_provider];
    public List<IServiceCollection> DisposableContainers { get; } = [];

    public object GetService(Type serviceType)
    {
        return GetService(serviceType, false)!;
    }

    public object? GetService(Type serviceType, bool noCreate = false)
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
            List<IServiceCollection>? MyPlugins = null;
            lock (PluginContainers) MyPlugins = [..PluginContainers, ..DisposableContainers];

            foreach(var container in MyPlugins ?? [])
            {
                var isService = container.FindService(serviceType);
                if(isService?.Lifetime == ServiceLifetime.Scoped && !_scoped)
                {
                    if (noCreate) return null;
                    else
                        throw new InvalidOperationException("Trying to get a scoped services out of root container. Call sp.CreateScope() first.");
                }
                if (isService == null) continue;

                if (isService.Lifetime == ServiceLifetime.Scoped)
                    if (Disposables.TryGetValue(isService.ServiceType, out var existing))
                        return existing;

                if (isService.Lifetime == ServiceLifetime.Singleton)
                    if (Indisposables.TryGetValue(isService.ServiceType, out var existing))
                        return existing;

                if (isService.ImplementationType != null && isService.Lifetime == ServiceLifetime.Scoped)
                    if (Disposables.TryGetValue(isService.ImplementationType, out var existing))
                        return existing;

                if (isService.ImplementationType != null && isService.Lifetime == ServiceLifetime.Singleton)
                    if (Indisposables.TryGetValue(isService.ImplementationType, out var existing))
                        return existing;
            }

            if (PluginContainers.Count <= 1 && DisposableContainers.Count <= 1)
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!Plugins is null!!!!!!!!!!!!!!");

            if(serviceType == typeof(PluginsPage))
            {
                Console.WriteLine("Plugins created");
            }

            if (noCreate) return null;

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

    static Dictionary<Type, List<Type>>? serviceTypes;

    static CompositeServiceProvider()
    {
        lock (TrustedLoader.CachedState)
            serviceTypes = TrustedLoader.CachedState.StoredServiceable.ToDictionary();
    }




    public object? CreateService(Type serviceType)
    {
        var collection = new ServiceCollection();

        lock(TrustedLoader.CachedState)
            serviceTypes = TrustedLoader.CachedState.StoredServiceable.ToDictionary();

        var baseType = serviceTypes.FirstOrDefault(kvp => kvp.Key.Extends(serviceType)).Key
            ?? serviceTypes.FirstOrDefault(kvp => kvp.Value.Any(inter => inter == serviceType)).Key;

        if (baseType == null) return null;

        var constructors = baseType.GetConstructors();

        var moreRequirements = constructors
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        var moreServices = serviceTypes
            .Where(kvp => moreRequirements.Any(req => kvp.Key.Extends(req)
                || kvp.Value.Any(inter => inter.Extends(req))))
            .Select(kvp => kvp.Key)
            .ToList();

        List<Type> existingTypes = [.. BuiltIn.Concat(UserTypes).Distinct()];
        AddExisting(this, collection, existingTypes);
        collection.BuildServices([baseType, .. moreServices, .. existingTypes], null, this, true);

        foreach (var t in collection) 
            if (!existingTypes.Contains(t.ServiceType)) 
                UserTypes.Add(t.ServiceType);


        lock (PluginContainers)
        {
            if (_scoped)
                DisposableContainers.Add(collection);
            else
                PluginContainers.Add(collection);
        }
        var isService = InjectionExtensions.FindService(collection, serviceType);
        if (isService?.Lifetime == ServiceLifetime.Scoped && !_scoped)
        {
            throw new InvalidOperationException("Trying to get a scoped services out of root container. Call sp.CreateScope() first. " + serviceType);
        }
        var serviceObject = InjectionExtensions.CreateFromDescriptor(isService, this, isService?.Lifetime == ServiceLifetime.Transient);

        if (serviceObject != null && isService?.Lifetime == ServiceLifetime.Scoped)
            Disposables.Add(isService.ServiceType, serviceObject);

        if (serviceObject != null && isService?.Lifetime == ServiceLifetime.Singleton)
            if (_scoped)
                (PluginContainers[0] as CompositeServiceProvider)?.Indisposables.Add(isService.ServiceType, serviceObject);
            else
                Indisposables.Add(isService.ServiceType, serviceObject);

        return serviceObject;
    }



    protected static void AddExisting(CompositeServiceProvider Provider, IServiceCollection collection, List<Type> checkExisting)
    {
        var types = checkExisting.Concat(checkExisting.SelectMany(e => e.GetInterfaces()));
        foreach (var ass in types)
        {
            try
            {
                if (Provider.GetService(ass, true) != null)
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
        var scoped = new CompositeServiceProvider(this, true);
        scoped.PluginContainers.Add(this);
        return scoped;
    }

    public void Dispose()
    {
        foreach(var container in DisposableContainers)
        {
            if (container is ICompositeProvider composite)
                composite.Dispose();
            if (container is IAsyncDisposable asyncTrash)
                _ = asyncTrash.DisposeAsync(); // i give a f, tasks have always been set and forget to me
        }

        foreach (var disposable in Disposables)
        {
            if (disposable.Value is IAsyncDisposable asyncTrash)
                _ = asyncTrash.DisposeAsync();
            if (disposable.Value is IDisposable trash)
                trash.Dispose();
        }

        Disposables.Clear();
        DisposableContainers.Clear();
        PluginContainers.Clear();

        GC.SuppressFinalize(this);
    }

    public bool IsService(Type serviceType)
    {
        //if (services?.ContainsKey(serviceType) == true
        //    || services?.Any(s => s.Value.Contains(serviceType)) == true)
        //    return true;
        List<IServiceCollection> containers = [.. PluginContainers, .. DisposableContainers];
        foreach (var container in containers)
        {
            if (InjectionExtensions.FindService(container, serviceType) != null)
            {
                return true;
            }
        }
        return false;
    }

    public static List<Type> BuiltIn { get; } = [
        //typeof(PluginActivator),
        typeof(CompositeServiceProvider),
        //typeof(RenderStateProvider),
        typeof(Atrium.Components.MainLoader)
    ];


    public List<Type> SingleUser { get; } = [
        typeof(HttpClient),
        //typeof(NavigationManager),
        //typeof(IJSRuntime),
        typeof(IConfiguration),
        //{typeof(ILogger<>), typeof(Logger<>)  },
        typeof(ILoggerFactory),
#if !BROWSER
        typeof(Lazy<Atrium.Components.MainLoader?>),
        typeof(Lazy<Application?>),
        typeof(Microsoft.Extensions.Hosting.IHostEnvironment),
#endif
    ];

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



    public static ServiceDescriptor? FindService(this IServiceCollection container, Type serviceType)
    {
        var isService = container.FirstOrDefault(s => s.ServiceType == serviceType
        || s.ImplementationType == serviceType);
        //?? container.FirstOrDefault(s => s.ServiceType.Extends(serviceType)
        //|| s.ImplementationType.Extends(serviceType));
        return isService;
    }



    public static object? CreateFromDescriptor(this ServiceDescriptor? isService, IServiceProvider service, bool transient)
    {
        try
        {
            if (!transient && isService?.ImplementationInstance != null)
            {
                return isService.ImplementationInstance;
            }
        }
        catch { }

        try
        {
            if (!transient && isService?.KeyedImplementationInstance != null)
            {
                return isService.KeyedImplementationInstance;
            }
        } catch { }


        try
        {
            if (isService?.ImplementationFactory != null)
            {
                return isService.ImplementationFactory(service);
            }
        } catch { }


        try
        {
            if (isService?.KeyedImplementationFactory != null)
            {
                return isService.KeyedImplementationFactory(service, isService.ServiceKey);
            }
        } catch { }


        try
        {
            if (isService?.ImplementationType != null)
            {
                return ActivatorUtilities.CreateInstance(service, isService.ImplementationType);
            }
        } catch { }


        try
        {
            if (isService?.ServiceType.IsConcrete() == true)
            {
                return ActivatorUtilities.CreateInstance(service, isService.ServiceType);
            }
        } catch { }

        return null;
    }

}


// step 2. fuck component di

#if false

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

        IComponent? serviceComponent;
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


#endif
