using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Interfacing.Services;

public static class InvokableExtensions
{

    public static object? InvokeService(this Delegate? myDelegate, IServiceProvider? service, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        return myDelegate.Method.InvokeService(service, myDelegate.Target, args);
    }

    public static object? InvokeService(this MethodInfo? myDelegate, IServiceProvider? service, object? thisObject = null, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        var formFactor = service?.GetService(typeof(IFormFactor)) as IFormFactor;
        var parameters = myDelegate.GetParameters();
        var parameterValues = new object?[parameters.Length];
        var Scope = service?.CreateScope();
        for (int i = 0; i < parameters.Length; i++)
        {
            var realType = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
            // TODO: add more special service injection handlers
            if (parameters[i].ParameterType == typeof(Type) && string.Equals(parameters[i].Name, "routeControl"))
            {
                try
                {
                    parameterValues[i] = formFactor?.RequestControl;
                }
                catch { }
            }
            else if (args?.ElementAtOrDefault(i) == null && parameters[i].IsNullable()
                && !parameters[i].IsService())
            {
                parameterValues[i] = null;
            }
            // TODO: find a way to match parameter names to a dictionary passed in or query params?
            else if (args?.ElementAtOrDefault(i) is object obj
                && obj.GetType().Extends(realType))
            {
                parameterValues[i] = obj;
            }
            else if (args?.FirstOrDefault(a => a?.GetType().Extends(realType) == true) is object obj2)
            {
                parameterValues[i] = obj2;
            }
            else if (!string.IsNullOrEmpty(parameters[i].Name)
                && formFactor?.QueryParameters?.TryGetValue(parameters[i].Name!, out var param) == true)
            {
                parameterValues[i] = param;
            }
            else
            {
                parameterValues[i] = Scope?.ServiceProvider.GetService(realType);
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




    public static bool IsServiceable(this ParameterInfo type)
    {
        return type.ParameterType.IsConcrete() && type.ParameterType.GetInterfaces().Any(IsService);
    }


    public static bool IsServiceable(this Type type)
    {
        return type.IsConcrete() && type.GetInterfaces().Any(IsService);
    }


    public static bool IsService(this ParameterInfo type)
    {
        return type.ParameterType.IsService() || type.ParameterType.GetInterfaces().Any(IsService);
    }

    public static bool IsService(this Type t)
    {
        if (t.Extends(typeof(IHasNoService))) return false;
        return 
            t.Name.Contains("Service", StringComparison.InvariantCultureIgnoreCase)
            || t.Namespace?.Contains("Service", StringComparison.InvariantCultureIgnoreCase) == true
            || t.Extends(typeof(IHasService))
            || t.Extends(typeof(IHasCurrent<>))
            || t.Extends(typeof(IHasPlugins))
            || t.Extends(typeof(IHasFeatures));
    }


    public static List<Type> GetServiceable(this IEnumerable<Type> asses)
    {
        return [.. asses.Where(IsServiceable).Distinct()];
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
