using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using System.Collections.Generic;

namespace Atrium.Extensions;

internal static class ComponentExtensions
{

#pragma warning disable BL0006 // Do not use RenderTree types
    public static Renderer? Renderer(this IComponent component)
    {
        FieldInfo? handleField = null;
        if (component.GetType().IsConstructedGenericType 
            && component.GetType().GetGenericTypeDefinition() == typeof(CascadingValue<>))
            handleField = typeof(CascadingValue<>)
                .GetField("_renderHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        else if (typeof(ComponentBase).IsAssignableFrom(component.GetType()))
            handleField = typeof(ComponentBase)
                .GetField("_renderHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        var handle = handleField?.GetValue(component);

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


