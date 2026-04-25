using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;

namespace Atrium.Extensions;

internal static class ComponentExtensions
{

    // oh good, component singleton means this will work


    public static async Task<string> ToHtml(this IComponent? service)
    {
        if (service == null) return string.Empty;
        var composite = service.Renderer()?.Service()?.GetService<ICompositeProvider>();
        if (service is IHasRender Render)
        {
            return await ((RenderFragment)Render.Render(composite)).ToHtml(composite);
        }
        RenderFragment Fragment = __builder =>
        {
            __builder.OpenComponent(0, service.GetType());
            __builder.CloseComponent();
        };
        return await Fragment.ToHtml(composite);
    }


    // and this


    public static async Task<string> ToHtml(this Type? service, ICompositeProvider? serviceProvider)
    {
        if (service == null || !service.Extends(typeof(IComponent))) return string.Empty;
        if (service.Extends(typeof(IHasRender))
            && serviceProvider?.GetService(service) is IHasRender Render)
        {
            return await ((RenderFragment)Render.Render(serviceProvider)).ToHtml(serviceProvider);
        }
        if (service.Extends(typeof(IAsyncRender))
            && serviceProvider?.GetService(service) is IAsyncRender Render2)
        {
            if(await Render2.Render(serviceProvider) is RenderFragment task)
                return await task.ToHtml(serviceProvider);
        }
        RenderFragment Fragment = __builder =>
        {
            __builder.OpenComponent(0, service);
            __builder.CloseComponent();
        };
        return await Fragment.ToHtml(serviceProvider);
    }



    public static async Task<string> ToHtml(this RenderFragment? fragment, IServiceProvider? serviceProvider = null)
    {
        if (fragment == null) return string.Empty;
        serviceProvider ??= new ServiceCollection().AddLogging().BuildServiceProvider();
        // TODO: prefer composite?
        serviceProvider = serviceProvider.GetService<ICompositeProvider>() ?? serviceProvider;
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // Use the private wrapper defined below
            var output = await renderer.RenderComponentAsync<FragmentWrapper>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                { nameof(FragmentWrapper.Content), fragment }
                })
            );

            return output.ToHtmlString();
        });
    }

    // Private helper to satisfy the IComponent requirement
    private class FragmentWrapper : ComponentBase
    {
        [Parameter] public RenderFragment Content { get; set; } = default!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, Content);
    }

#pragma warning disable BL0006 // Do not use RenderTree types
    public static RenderHandle? Handle(this IComponent component)
    {
        FieldInfo? handleField = null;
        if (component.GetType().IsConstructedGenericType
            && component.GetType().GetGenericTypeDefinition() == typeof(CascadingValue<>))
            handleField = typeof(CascadingValue<>)
                .GetField("_renderHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        else if (typeof(ComponentBase).IsAssignableFrom(component.GetType()))
            handleField = typeof(ComponentBase)
                .GetField("_renderHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);


        if (handleField?.GetValue(component) is not RenderHandle handle) return null;
        return handle;
    }


    public static ICompositeProvider? Service(this Renderer renderer)
    {
        var servicesProperty = renderer.GetType().GetField("_serviceProvider", BindingFlags.NonPublic);

        return servicesProperty?.GetValue(renderer) as ICompositeProvider;
    }


    public static Renderer? Renderer(this IComponent component)
    {
        var handle = component.Handle();

        if (handle == null) return null;
        
        // 2. RenderHandle is a struct. We need to get the private _renderer field inside it.
        var rendererField = typeof(RenderHandle).GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance);
        var renderer = rendererField?.GetValue(handle);

        if (renderer == null) return null;

        return renderer as Renderer;
    }

    public static Dictionary<int, ComponentState>? State(this Renderer? renderer)
    {
        if (renderer == null) return null;

        // 1. Get the correct field name from your source: _componentStateById
        var stateMapField = typeof(Renderer).GetField("_componentStateById", BindingFlags.NonPublic | BindingFlags.Instance);

        if (stateMapField?.GetValue(renderer) is not Dictionary<int, ComponentState> stateMap)
            return null;
        return stateMap;
    }


#pragma warning restore BL0006 // Do not use RenderTree types


    public static ComponentState? State(this IComponent? parent)
    {
        if (parent == null) return null;
        var renderer = parent.Renderer(); // Using our previous reflection helper
        var stateMap = renderer.State();
        return stateMap?.Values.FirstOrDefault(state => state.Component == parent) is ComponentState state ? state : null;
    }



    public static IComponent? Parent(
        this IComponent child)
    {
        try
        {
            var myState = child.State();
            return myState?.ParentComponentState?.Component;
        }
        catch
        {
            // Fail-safe: A.R.S. § 44-7007 Reliability Fallback
            return null;
        }
    }

    public static List<IComponent> GetChildComponents(this IComponent parent, Dictionary<int, ComponentState>? stateMap = null)
    {
        List<IComponent> result = [];
        try
        {
            stateMap ??= parent.Renderer()?.State();
            var myId = stateMap?.FirstOrDefault(state => state.Value.Component == parent).Value?.ComponentId
                ?? parent.State()?.ComponentId;
            var children = stateMap?.Values
                .Where(state => state.ParentComponentState?.ComponentId == myId)
                .ToList();

            foreach(var entry in children ?? [])
            {
                var type = entry.Component.GetType();
                if(entry.Component is LayoutView
                        || typeof(LayoutComponentBase).IsAssignableFrom(type)
                        || typeof(ErrorBoundaryBase).IsAssignableFrom(type)
                        || type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(CascadingValue<>))
                {
                    
                }
                else
                {
                    result.Add(entry.Component);
                }
                var moreChildren = GetChildComponents(entry.Component, stateMap);
                result.AddRange(moreChildren);
            }
        }
        catch
        {
        }
        return result;
    }


}


