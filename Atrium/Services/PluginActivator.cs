using Microsoft.AspNetCore.Components;

namespace Atrium.Services;

public class PluginActivator(IServiceProvider mainProvider, IServiceProvider registry) : IComponentActivator
{
    public IComponent CreateInstance(Type componentType)
    {
        var composite = new CompositeServiceProvider(registry, mainProvider);
        return (IComponent)ActivatorUtilities.CreateInstance(composite, componentType);
    }
}

public partial class CompositeServiceProvider(IServiceProvider pluginProvider, IServiceProvider mainProvider) : IServiceProvider, ISupportRequiredService
{
    public object GetService(Type serviceType)
    {
        // The "Wizard" logic: check plugin first, then fallback
        return pluginProvider.GetService(serviceType)
               ?? mainProvider.GetService(serviceType)!;
    }

    public object GetRequiredService(Type serviceType)
    {
        var service = GetService(serviceType) 
            ?? throw new InvalidOperationException($"Service {serviceType} not found in either container.");
        return service;
    }
}