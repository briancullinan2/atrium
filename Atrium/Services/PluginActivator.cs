using Interfacing.Services;
using Microsoft.AspNetCore.Components;

namespace Atrium.Services;

public class PluginActivator : IComponentActivator, IServiceProviderIsService, IHasService //, IHasCurrent<PluginActivator> // Current is null
{
    private readonly IServiceProvider Main;
    private readonly IServiceScope Scope;

    public IServiceProvider Services { get; private set; }

    public PluginActivator(IServiceProvider mainProvider)
    {
        Main = mainProvider;
        Services = new CompositeServiceProvider(this, mainProvider);
        Scope = Services.CreateScope();
    }


    // TODO: replace Presentation with an extended type selected by main layout or query string

    public IComponent CreateInstance(Type componentType)
    {
        /*if (componentType.GetInterfaces().Any(inter => inter == typeof(IHasCurrent<RenderFragment>)) {
            var frag = (GetType().GetProperty("Current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as RenderFragment)
            return componentType.
        }*/

        var instance = (IComponent)ActivatorUtilities.CreateInstance(Services, componentType);

        var properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() != null);

        foreach (var prop in properties)
        {
            var service = Scope.ServiceProvider.GetService(prop.PropertyType);
            if (service != null)
            {
                prop.SetValue(instance, service);
            }
        }

        // TODO: IHasCurrent, always use Current IComponent instead of creating a new one

        return instance;
    }

    public bool IsService(Type serviceType)
    {
        return
            ((CompositeServiceProvider)Services).PluginPopin?.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true
            || Main.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true;
    }
}

public partial class CompositeServiceProvider(
IServiceProviderIsService _service,
IServiceProvider _provider) : 
    IServiceProvider
    , ISupportRequiredService
    , IHasService
    , IServiceScopeFactory
    , ICompositeProvider
{
    public IServiceProvider Services => this;
    // something you got to introduce a little... anarchy
    public IServiceProvider? PluginPopin { get; set; } = null;

    public object GetService(Type serviceType)
    {
        if (serviceType == typeof(CompositeServiceProvider))
            return this;
        if (serviceType == typeof(ICompositeProvider))
            return this;
        if (serviceType == typeof(IServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return _service;
        if (serviceType == typeof(IServiceScopeFactory))
            return this;

        // The "Wizard" logic: check plugin first, then fallback
        try
        {
            return
                PluginPopin?.GetService(serviceType)
                ?? _provider.GetService(serviceType)!;

        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }

    }

    public object GetRequiredService(Type serviceType)
    {
        var service = GetService(serviceType)
            ?? throw new InvalidOperationException($"Service {serviceType} not found in either container.");
        return service;
    }

    public IServiceScope CreateScope()
    {
        return new CompositeServiceScope(PluginPopin?.CreateScope(), _provider.CreateScope(), _service);
    }
}

internal partial class CompositeServiceScope(IServiceScope? primary, IServiceScope fallback, IServiceProviderIsService isService) : IServiceScope
{
    private readonly IServiceScope? _primaryScope = primary;
    private readonly IServiceScope _fallbackScope = fallback;
    private readonly IServiceProviderIsService _isService = isService;
    public IServiceProvider? StoredServiceProvider = null;
    public IServiceProvider ServiceProvider
    {
        get
        {
            return StoredServiceProvider ??= new CompositeServiceProvider(_isService, _fallbackScope.ServiceProvider)
            {
                PluginPopin = _primaryScope?.ServiceProvider
            };
        }
    }

    public void Dispose()
    {
        _primaryScope?.Dispose();
        _fallbackScope.Dispose();
    }
}