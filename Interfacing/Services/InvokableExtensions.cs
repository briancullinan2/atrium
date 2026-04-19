using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Interfacing.Services;

public static class InvokableExtensions
{

    public static object? InvokeService(this Delegate? myDelegate, ICompositeProvider service, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        return myDelegate.Method.InvokeService(service, myDelegate.Target, args);
    }

    public static object? InvokeService(this MethodInfo? myDelegate, ICompositeProvider service, object? thisObject = null, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        var formFactor = service.GetService(typeof(IFormFactor)) as IFormFactor;
        var parameters = myDelegate.GetParameters();
        var parameterValues = new object?[parameters.Length];
        var Scope = service.CreateScope();
        var rendered = Scope.ServiceProvider.GetRequiredService<IRenderState>();
        for (int i = 0; i < parameters.Length; i++)
        {
            var realType = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
            // TODO: add more special service injection handlers
            if (parameters[i].ParameterType == typeof(Type) && string.Equals(parameters[i].Name, "routeControl"))
            {
                try
                {
                    var nav = Scope.ServiceProvider.GetRequiredService<IFormFactor>();
                    parameterValues[i] = nav.RequestControl;
                }
                catch { }
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
            else if (args?.FirstOrDefault(a => a?.GetType().Extends(realType) == true) is object obj2)
            {
                parameterValues[i] = Convert.ChangeType(obj2, realType);
            }
            else if (!string.IsNullOrEmpty(parameters[i].Name)
                && rendered.IsReady // don't touch NavigationManager until its ready or it will complain
                && formFactor?.QueryParameters?.TryGetValue(parameters[i].Name!, out var param) == true)
            {
                parameterValues[i] = Convert.ChangeType(param, realType);
            }
            else
            {
                parameterValues[i] = Scope.ServiceProvider.GetService(realType);
            }
        }

        if (thisObject != null && !myDelegate.IsStatic)
        {
            return myDelegate.Invoke(thisObject, parameterValues);
        }
        return myDelegate.Invoke(null, thisObject != null ? [thisObject, .. parameterValues] : parameterValues);
    }


    public static bool IsNullable(this ParameterInfo parameter)
    {
        var context = new NullabilityInfoContext();

        var paramInfo = context.Create(parameter);
        if (paramInfo.WriteState == NullabilityState.Nullable)
        {
            return true;
        }
        return false;
    }


    public static List<Type> GetServicable(this IEnumerable<Type> asses)
    {

        List<Type> plugins = [..asses
            .Where(t => t.IsConcrete() && t.Extends(typeof(IHasPlugins)))
            .SelectMany(t => t.GetProperty(nameof(IHasPlugins.Plugins), BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as List<Type> ?? [])];

        asses = [.. asses.Concat(plugins)];

        List<Type> concrete = [.. asses.Where(s => s.IsConcrete() && !s.Extends(typeof(IHasNoService)))];

        List<string> interfaces = [..asses
            .Where(s => s.IsInterface)
            .Select(i => i.Name)
            ];

        List<Type> servicable = [..concrete
            .Where(c => c.GetInterfaces()
                .Select(i => i.Name)
                .Intersect(interfaces) // Finds names present in both lists
                .Any())                // Returns true if the intersection isn't empty
            ];

        /*List<Type> currents = [..servicable
            .Where(t => t.Extends(typeof(IHasCurrent<>)))
            .Select(t => {
                var interf = t.GetInterfaces().First(i => i.Extends(typeof(IHasCurrent<>)));
                return interf.GetGenericArguments().First();
            })
            .Where(t => t.IsConcrete())];
        */

        return [.. servicable.Distinct()];
    }


    public static bool Extends([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type? type, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type? genericDefinition)
    {
        if (type == null || genericDefinition == null) return false;

        // 1. Direct/Standard Check (Handles non-generics and exact matches)
        if (genericDefinition.IsAssignableFrom(type) || type.IsAssignableFrom(genericDefinition))
            return true;

        // 2. Generic Family Check
        // Get the "Open" version (e.g., Entity<Setting> -> Entity<>)
        var openDef = genericDefinition.IsGenericType
            ? genericDefinition.GetGenericTypeDefinition()
            : genericDefinition;

        var current = type;
        while (current != null && current != typeof(object))
        {
            // Check if the current type in hierarchy is the generic we're looking for
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openDef)
                return true;

            current = current.BaseType;
        }

        // 3. Check all implemented interfaces for a generic match
        if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openDef))
            return true;

        // 4. Reverse Check (In case we passed the open generic as the first argument)
        if (type.IsGenericType)
        {
            var openType = type.GetGenericTypeDefinition();
            if (genericDefinition.IsAssignableFrom(openType) ||
                genericDefinition.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openType))
                return true;
        }

        return false;
    }

    public static bool IsConcrete(this Type type)
    {
        if (type == null) return false;

        return !type.IsAbstract &&
               !type.IsInterface &&
               !type.IsGenericTypeDefinition;
    }

}
