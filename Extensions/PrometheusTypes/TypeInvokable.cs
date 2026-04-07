

namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {

        public static object? InvokeService(this Delegate? myDelegate, IServiceProvider service, params object?[]? args)
        {
            if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
            return myDelegate.Method.InvokeService(service, args);
        }

        public static object? InvokeService(this MethodInfo? myDelegate, IServiceProvider service, params object?[]? args)
        {
            if(myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
            var formFactor = service.GetService(typeof(IFormFactor)) as IFormFactor;
            var parameters = myDelegate.GetParameters();
            var parameterValues = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var realType = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
                // TODO: add more special service injection handlers
                if (parameters[i].ParameterType == typeof(Type) && string.Equals(parameters[i].Name, "routeControl"))
                {
                    var nav = service.GetRequiredService<NavigationManager>();
                    parameterValues[i] = IdentifyNavigation(nav.Uri);
                }
                else if (args?.ElementAtOrDefault(i) == null && parameters[i].IsNullable())
                {
                    parameterValues[i] = null;
                }
                // TODO: find a way to match parameter names to a dictionary passed in or query params?
                else if (args?.ElementAtOrDefault(i) is object obj
                    && obj.GetType().Extends(realType))
                {
                    parameterValues[i] = Convert.ChangeType(obj, realType);
                }
                else if (args?.FirstOrDefault(a => a.GetType().Extends(realType)) is object obj2)
                {
                    parameterValues[i] = Convert.ChangeType(obj2, realType);
                }
                else if (!string.IsNullOrEmpty(parameters[i].Name)
                    && formFactor?.QueryParameters?.ContainsKey(parameters[i].Name!) is object queryParameter)
                {
                    parameterValues[i] = Convert.ChangeType(queryParameter, realType);
                }
                else
                {
                    parameterValues[i] = service.GetService(realType);
                }
            }

            return myDelegate.Invoke(null, parameterValues);
        }

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

            var dispatcher = renderer.GetType()
                .GetProperties("Dispatcher").FirstOrDefault()
                ?.GetValue(renderer) as Dispatcher
                ?? throw new InvalidOperationException("Could not render renderer dispatcher.");

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

                    var isDisposed = (bool?)typeof(ComponentState).GetProperties("Disposed").FirstOrDefault()?.GetValue(entry) ?? false;
                    if (isDisposed) continue;

                    // 2. The "Handshake" Check: Is this a child OR a valid sibling?
                    if (entryParentId == myId || (siblingRootId != -1 && entryParentId == siblingRootId))
                    {
                        // Skip ourselves, framework noise, and the "Shared" furniture
                        if (comp == parent)
                            continue;

                        if (comp.GetType().Namespace?.Contains("Shared", StringComparison.InvariantCultureIgnoreCase) == true)
                            continue;

                        // 4. Recursive Digging for Container Types
                        var type = comp.GetType();
                        if (comp is LayoutView or AuthorizeView or AuthorizeRouteView or AuthorizeViewCore
                            || type.Extends(typeof(LayoutComponentBase))
                            || type.Extends(typeof(ErrorBoundaryBase)))
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




        public static IJSRuntime? Runtime(this IComponent component)
        {
            return component.Service()?.GetService<IJSRuntime>() ?? component.GetType()
                .GetProperties(null)
                .FirstOrDefault(p => p.PropertyType == typeof(IJSRuntime) ||
                                     p.PropertyType.IsAssignableTo(typeof(IJSRuntime)))
                ?.GetValue(component) as IJSRuntime;
        }

        
        public static ValueTask Invokable(this object? component, IJSRuntime? JS = null, IServiceProvider? Service = null)
        {
            if (component == null) return ValueTask.CompletedTask;

            var type = component.GetType();

            return Invokable(type, JS, Service);
        }


        public static ValueTask Invokable(this Type type, IJSRuntime? JS = null, IServiceProvider? Service = null)
        {
            if (type == null) return ValueTask.CompletedTask;

            var methodNames = type.GetMethods(null)
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

            var type = component.GetType();
            var clear = type.Namespace?.Split('.').FirstOrDefault()
                ?? type.FullName ?? type.Name;
            await JS.InvokeVoidAsync("interconnect.clear", clear);

            foreach (var comp in currentComponents)
            {
                var id = comp.GetId();
                await comp.Invokable(JS);
            }
        }

    }
}
