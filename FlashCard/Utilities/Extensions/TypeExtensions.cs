using DataLayer.Utilities.Extensions;
using FlashCard.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FlashCard.Utilities.Extensions
{
    public static class TypeExtensions
    {

#pragma warning disable BL0006 // Do not use RenderTree types
        public static Renderer? Renderer(this IComponent component)
        {
            var handleField = component.GetType().GetFields("_renderHandle").FirstOrDefault();
            var handle = handleField?.GetValue(component);

            if (handle == null) return null;

            // 2. RenderHandle is a struct. We need to get the private _renderer field inside it.
            var rendererField = typeof(RenderHandle).GetFields("_renderer").FirstOrDefault();
            var renderer = rendererField?.GetValue(handle);

            if (renderer == null) return null;

            return renderer as Renderer;
        }


        public static IServiceProvider? Service(this Renderer renderer)
        {
            var servicesProperty = renderer.GetType().GetFields("_serviceProvider").FirstOrDefault();

            return servicesProperty?.GetValue(renderer) as IServiceProvider;
        }


        public static Dictionary<int, ComponentState>? State(this Renderer? renderer)
        {
            if (renderer == null) return null;

            // 1. Get the correct field name from your source: _componentStateById
            var stateMapField = renderer.GetType().GetFields("_componentStateById").FirstOrDefault();

            if (stateMapField?.GetValue(renderer) is not Dictionary<int, ComponentState> stateMap)
                return null;
            return stateMap;
        }


        public static Task AfterRender(this Renderer renderer, Action? onRendered = null)
        {
            if (renderer == null) return Task.CompletedTask;

            // 1. Get the internal 'OnRenderCompleted' Task or Event
            // In 2026, many Renderers have an internal '_lastBatchTask' or similar.
            // A more reliable way is to hook the Dispatcher's work queue.

            if (renderer.GetType()
                .GetProperty("Dispatcher", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(renderer) is not Dispatcher dispatcher) return Task.CompletedTask;

            // 2. The Trick: We can't override the method, but we can schedule a 
            // "Shadow Task" on the dispatcher that runs after the current render batch.
            dispatcher.CheckAccess(); // Ensure we are on the right thread

            // We use a recurring 'Yield' to check if the component state has updated.
            return Task.Run(async () =>
            {
                //while (!component.IsDisposed())
                {
                    // Wait for the Dispatcher to be 'Idle' (meaning render is done)
                    await dispatcher.InvokeAsync(() =>
                    {
                        onRendered?.Invoke();
                    });

                    // "Stay of Execution": Don't spam the CPU. 
                    // Only check when the Renderer actually signals a change.
                    await Task.Delay(100);
                }
            });
        }


        public static Dictionary<int, ComponentState>? State(this IServiceProvider? service)
        {
            return (service?.GetService(typeof(Renderer)) as Renderer).State();
        }


#pragma warning restore BL0006 // Do not use RenderTree types


        public static Task AfterRender(this IComponent component, Action? onRendered = null)
        {
            var renderer = component.Renderer();
            return renderer?.AfterRender(onRendered) ?? Task.CompletedTask;
        }


        public static IServiceProvider? Service(this IComponent component)
        {
            return component.Renderer()?.Service();
        }


        public static ComponentState? ByType(this Dictionary<int, ComponentState>? stateMap, Type? type)
        {
            if (stateMap == null) return null;
            foreach (var entry in stateMap.Values)
            {
                var comp = entry.Component;

                var isDisposed = (bool?)typeof(ComponentState).GetProperties("Disposed").FirstOrDefault()?.GetValue(entry) ?? false;
                if (isDisposed) continue;

                if (comp.GetType() == type)
                    return entry;
            }
            return null;
        }


        public static ComponentState? State(this IComponent? parent)
        {
            if (parent == null) return null;
            var renderer = parent.Renderer(); // Using our previous reflection helper
            var stateMap = renderer.State();
            return stateMap?.Values.FirstOrDefault(state => state.Component == parent) is ComponentState state ? state : null;
        }

        public static int GetId(this IComponent parent)
        {
            return parent.State()?.ComponentId ?? -1;
        }






        public static List<IComponent> GetChildComponents(this IComponent parent, bool includeSiblings = true)
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
                var sharedNamespace = typeof(Pages.Shared.Console).Namespace;

                List<IComponent> result = [];

                foreach (var entry in stateMap.Values)
                {
                    var entryParentId = entry.ParentComponentState?.ComponentId;
                    var comp = entry.Component;

                    var isDisposed = (bool?)typeof(ComponentState).GetProperties("Disposed").FirstOrDefault()?.GetValue(entry) ?? false;
                    if (isDisposed) continue;

                    // 2. The "Handshake" Check: Is this a child OR a valid sibling?
                    if (entryParentId == myId || (siblingRootId != -1 && entryParentId == siblingRootId))
                    {
                        // Skip ourselves, framework noise, and the "Shared" furniture
                        if (comp == parent || comp is NavMenu)
                            continue;

                        if (comp.GetType().Namespace == sharedNamespace)
                            continue;

                        // 4. Recursive Digging for Container Types
                        var typeName = comp.GetType().Name;
                        if (comp is LayoutView or AuthorizeView or AuthorizeRouteView or MainLayout or AuthorizeViewCore or ErrorBoundary)
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


        public static IComponent? Component(this RenderFragment? fragment)
        {
            var target = fragment?.Target;

            // 2. In Blazor 2026, the Body delegate target is often 
            // a 'RouteView' or a compiler-generated closure.
            if (target is RouteView view)
            {
                var renderer = view.Renderer();
                var stateMap = renderer.State() ?? [];
                if (stateMap.ByType(view.RouteData.PageType) is ComponentState state)
                    target = state.Component;
                else
                {
                    var child = view.GetChildComponents(true);
                    if (child.FirstOrDefault() is IComponent actualPage)
                        target = actualPage;
                }
            }

            if (target is IComponent component) return component;

            if (target != null)
            {
                // The "Deep Magic": If it's a closure, find the captured component field
                // Usually named 'childComponent' or 'Component' in the RouteView context
                var componentField = target.GetType()
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(f => typeof(IComponent).IsAssignableFrom(f.FieldType));

                if (componentField?.GetValue(target) is IComponent capturedPage)
                {
                    return capturedPage;
                }
            }

            return null;
        }


        public static IJSRuntime? Runtime(this IComponent component)
        {
            return component.Service()?.GetService<IJSRuntime>() ?? component.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(IJSRuntime) ||
                                     p.PropertyType.IsAssignableTo(typeof(IJSRuntime)))
                ?.GetValue(component) as IJSRuntime;
        }

        public static ValueTask Invokable(this object? component, IJSRuntime? JS = null, IServiceProvider? Service = null)
        {
            if (component == null) return ValueTask.CompletedTask;

            var type = component.GetType();
            var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                  .Select(m => m.Name)
                                  .ToArray();

            var path = type.FullName ?? type.Name;

            // 2. Wrap and Pin
            var sentry = new InterconnectSentry(component, Service);
            var objRef = DotNetObjectReference.Create(sentry);

            // 3. Register with the Method Names list
            var await = JS?.InvokeVoidAsync("interconnect.register", path, objRef, methodNames, Service != null);

            return await ?? ValueTask.CompletedTask;
        }

        public static ValueTask Invokable(this Type type, IJSRuntime? JS = null, IServiceProvider? Service = null)
        {
            if (type == null) return ValueTask.CompletedTask;

            var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                  .Select(m => m.Name)
                                  .ToArray();

            var path = type.FullName ?? type.Name;

            // 2. Wrap and Pin
            var sentry = new InterconnectSentry(type, Service);
            var objRef = DotNetObjectReference.Create(sentry);

            // 3. Register with the Method Names list
            var await = JS?.InvokeVoidAsync("interconnect.register", path, objRef, methodNames, Service != null);

            return await ?? ValueTask.CompletedTask;
        }


        public static ValueTask Invokable(this IComponent component, IJSRuntime JS)
        {
            if (component is RouteView or AuthorizeRouteView)
            {
                return ValueTask.CompletedTask;
            }

            var js = JS ?? component.Runtime();

            if (js == null) return ValueTask.CompletedTask;

            return Invokable((object)component, js, component.Service());
        }


        public class InterconnectSentry(object target, IServiceProvider? Service)
        {

            [JSInvokable("Invokable")]
            public async Task<string?> Invokable(string typeName)
            {
                var type = typeName.ToType() ?? throw new InvalidOperationException("type not found: " + typeName);
                await type.Invokable(Service?.GetService(typeof(IJSRuntime)) as IJSRuntime, Service);
                return type.FullName ?? type.Name;
            }


            [JSInvokable("GetService")]
            public async Task<string?> GetService(string typeName)
            {
                var type = typeName.ToType() ?? throw new InvalidOperationException("type not found: " + typeName);
                var service = (Service?.GetService(type)) ?? throw new InvalidOperationException("service not found: " + type.FullName);
                await service.Invokable(Service?.GetService(typeof(IJSRuntime)) as IJSRuntime, Service);
                return service.GetType().FullName ?? service.GetType().Name;
            }


            [JSInvokable("Invoke")]
            public object? Invoke(string methodName, JsonElement[] args)
            {
                // 1. Reflection Handshake: Find the method on the 'target'
                var type = target as Type ?? target.GetType();
                var method = type.GetMethods(methodName)
                    .OrderBy(m => m.ContainsGenericParameters)
                    .FirstOrDefault()
                    ?? throw new MissingMethodException($"Sentry: Method '{methodName}' not found on {type.Name}.");

                //Console.WriteLine("Calling method: " + method.Name + " - " + string.Join(", ", method.GetParameters().Select(p => p.Name)));
                // 2. Security Check (The 'Whitelist' Logic)
                // Since we aren't using attributes, we might want to check if it's 
                // a 'safe' name or part of a specific interface.

                // 3. Type Marshalling: Convert JsonElement args to actual C# types
                var parameters = method.GetParameters();
                var convertedArgs = new object?[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i < args.Length)
                    {
                        convertedArgs[i] = args[i].Deserialize(parameters[i].ParameterType);
                    }
                }

                // 4. Execute and Return
                if (method.IsStatic || target is Type)
                {
                    return method.Invoke(null, convertedArgs);
                }
                else
                {
                    return method.Invoke(target, convertedArgs);
                }
            }
        }


        public static async Task Mirror(this IComponent component)
        {
            var JS = component.Runtime();
            if (JS == null) return;

            var currentComponents = new List<IComponent>([component]).Concat(component.GetChildComponents());
            var currentIds = currentComponents.ToDictionary(c => c.GetId(), c => c.GetType().FullName ?? c.GetType().Name);

            await JS.InvokeVoidAsync("interconnect.clear", nameof(FlashCard));

            foreach (var comp in currentComponents)
            {
                var id = comp.GetId();
                await comp.Invokable(JS);
            }
        }

    }
}
