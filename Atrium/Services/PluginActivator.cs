using Interfacing.Services;
using Microsoft.AspNetCore.Components;

namespace Atrium.Services;

public class PluginActivator : IComponentActivator, IServiceProviderIsService, IHasService
{
    private readonly IServiceProvider Main;
    private readonly CompositeServiceProvider Composite;

    public IServiceProvider Services => Composite;

    public PluginActivator(IServiceProvider mainProvider)
    {
        Main = mainProvider;
        Composite = new(this, mainProvider);
    }

    public IComponent CreateInstance(Type componentType)
    {
        return (IComponent)ActivatorUtilities.CreateInstance(Composite, componentType);
    }

    public bool IsService(Type serviceType)
    {
        return
            Composite.PluginPopin?.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true
            || Main.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true;
    }
}

public partial class CompositeServiceProvider(
    IServiceProviderIsService isService, 
    IServiceProvider mainProvider) : 
    IServiceProvider, ISupportRequiredService, IHasService, IServiceScopeFactory
{
    public IServiceProvider Services => this;
    // something you got to introduce a little... anarchy
    public IServiceProvider? PluginPopin { get; set; } = null;

    public object GetService(Type serviceType)
    {
        if (serviceType == typeof(CompositeServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProvider))
            return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return isService;
        if (serviceType == typeof(IServiceScopeFactory))
            return this;

        // The "Wizard" logic: check plugin first, then fallback
        try
        {
            return
                PluginPopin?.GetService(serviceType)
                ?? mainProvider.GetService(serviceType)!;

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
        return new CompositeServiceScope(PluginPopin?.CreateScope(), mainProvider.CreateScope(), isService);
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