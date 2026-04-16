
namespace DataShared.Extensions;

internal static partial class ExtendsExtensions
{

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

}
