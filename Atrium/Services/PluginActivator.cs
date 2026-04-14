using Interfacing.Services;
using Microsoft.AspNetCore.Components;

namespace Atrium.Services;

public class PluginActivator : IComponentActivator, IServiceProviderIsService, IHasService //, IHasCurrent<PluginActivator> // Current is null
{
    private readonly IServiceProvider Main;

    public IServiceProvider Services => CompositeServiceProvider.Current!;

    public static PluginActivator? Current { get; private set; } = null;

    public PluginActivator(IServiceProvider mainProvider)
    {
        Main = mainProvider;
        new CompositeServiceProvider(this, mainProvider);
        Current ??= this;
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
            var service = Services.GetService(prop.PropertyType);
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

public partial class CompositeServiceProvider : 
    IServiceProvider
    , ISupportRequiredService
    , IHasService
    , IServiceScopeFactory
    , ICompositeProvider
    , IHasCurrent<ICompositeProvider>
{
    private readonly IServiceProviderIsService IsService;
    private readonly IServiceProvider Provider;

    public CompositeServiceProvider(
    IServiceProviderIsService _service,
    IServiceProvider _provider)
    {
        IsService = _service;
        Provider = _provider;
        Current = this;
    }

    public IServiceProvider Services => this;
    // something you got to introduce a little... anarchy
    public IServiceProvider? PluginPopin { get; set; } = null;

    public static ICompositeProvider? Current { get; private set; } = null;

    public object GetService(Type serviceType)
    {
        if (serviceType == typeof(CompositeServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return IsService;
        if (serviceType == typeof(IServiceScopeFactory))
            return this;

        // The "Wizard" logic: check plugin first, then fallback
        try
        {
            return
                PluginPopin?.GetService(serviceType)
                ?? Provider.GetService(serviceType)!;

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
        return new CompositeServiceScope(PluginPopin?.CreateScope(), Provider.CreateScope(), IsService);
    }
}

internal partial class CompositeServiceScope : IServiceScope
{
    private readonly IServiceScope? _primaryScope;
    private readonly IServiceScope _fallbackScope;

    public CompositeServiceScope(IServiceScope? primary, IServiceScope fallback, IServiceProviderIsService isService)
    {
        _primaryScope = primary;
        _fallbackScope = fallback;
        // The ServiceProvider of the scope must ALSO be a composite!
        var scopedComposite = new CompositeServiceProvider(isService, _fallbackScope.ServiceProvider);
        ServiceProvider = scopedComposite;
        scopedComposite.PluginPopin = primary?.ServiceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    public void Dispose()
    {
        _primaryScope?.Dispose();
        _fallbackScope.Dispose();
    }
}