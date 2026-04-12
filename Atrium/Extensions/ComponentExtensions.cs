using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atrium.Extensions;

internal static class ComponentExtensions
{

#pragma warning disable BL0006 // Do not use RenderTree types
    public static Renderer? Renderer(this IComponent component)
    {
        var handleField = typeof(ComponentBase)
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


    public static List<IComponent> GetChildComponents(
        this IComponent parent, bool includeSiblings = true)
    {
        try
        {
            var myState = parent.State();
            var myId = myState?.ComponentId ?? -1;
            if (myId == -1) return [];

            // 1. Identify the "Search Roots"
            // If we're a Layout, we want children of OUR parent (our siblings) too.
            var siblingRootId = (includeSiblings && parent is LayoutComponentBase)
                ? myState?.ParentComponentState?.ComponentId ?? -1
                : -1;

            var renderer = parent.Renderer();
            var stateMap = renderer.State() ?? [];

            List<IComponent> result = [];

            foreach (var entry in stateMap.Values)
            {
                var entryParentId = entry.ParentComponentState?.ComponentId;
                var comp = entry.Component;

                //var isDisposed = (bool?)typeof(ComponentState).GetProperties("Disposed").FirstOrDefault()?.GetValue(entry) ?? false;
                //if (isDisposed) continue;

                // 2. The "Handshake" Check: Is this a child OR a valid sibling?
                if (entryParentId == myId || (siblingRootId != -1 && entryParentId == siblingRootId))
                {
                    // Skip ourselves, framework noise, and the "Shared" furniture
                    if (comp == parent)
                        continue;

                    // skip context menus and stuff
                    if (comp.GetType().Namespace?.Contains("Shared", StringComparison.InvariantCultureIgnoreCase) == true)
                        continue;

                    // 4. Recursive Digging for Container Types
                    var type = comp.GetType();
                    if (comp is LayoutView
                        || typeof(LayoutComponentBase).IsAssignableFrom(type)
                        || typeof(ErrorBoundaryBase).IsAssignableFrom(type))
                    {
                        // Dig deeper but DON'T look for siblings of the internal wrappers
                        var children = comp.GetChildComponents(includeSiblings: false);
                        result.AddRange(children);
                    }
                    else
                    {
                        if (!result.Contains(comp)) result.Add(comp);
                    }
                }
            }

            return result;
        }
        catch
        {
            // Fail-safe: A.R.S. § 44-7007 Reliability Fallback
            return [];
        }
    }


}


