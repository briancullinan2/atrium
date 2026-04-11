using Interfacing.Services;
using Microsoft.AspNetCore.Components;

namespace Atrium.Services;

public class PluginActivator(IServiceProvider mainProvider, IServiceProvider registry) : IComponentActivator, IHasService
{
    private readonly CompositeServiceProvider Composite = new(registry, mainProvider);

    public IServiceProvider Services => Composite;

    public IComponent CreateInstance(Type componentType)
    {
        return (IComponent)ActivatorUtilities.CreateInstance(Composite, componentType);
    }
}

public partial class CompositeServiceProvider(IServiceProvider pluginProvider, IServiceProvider mainProvider) : IServiceProvider, ISupportRequiredService, IHasService
{
    public IServiceProvider Services => this;
    // something you got to introduce a little... anarchy
    public IServiceProvider? PluginPopin { get; set; } = null;

    public object GetService(Type serviceType)
    {
        if(serviceType == typeof(CompositeServiceProvider))
            return this;

        // The "Wizard" logic: check plugin first, then fallback
        return 
            PluginPopin?.GetService(serviceType)
            ?? pluginProvider.GetService(serviceType)
            ?? mainProvider.GetService(serviceType)!;
    }

    public object GetRequiredService(Type serviceType)
    {
        var service = GetService(serviceType) 
            ?? throw new InvalidOperationException($"Service {serviceType} not found in either container.");
        return service;
    }
}