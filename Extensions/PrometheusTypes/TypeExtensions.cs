

using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;

namespace Extensions.PrometheusTypes
{
    // Token: 0x0200005E RID: 94
    public static partial class TypeExtensions
    {


        // Token: 0x060002F7 RID: 759 RVA: 0x00019228 File Offset: 0x00017428
        public static IEnumerable<Type> GetBaseTypes(this Type type)
        {
            IEnumerable<Type> result;
            if (type.BaseType != null)
            {
                result = new Type[]
                {
                type.BaseType
                }.Concat(type.BaseType.GetBaseTypes());
            }
            else
            {
                result = [];
            }
            return result;
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


        // Token: 0x060002F9 RID: 761 RVA: 0x000192F8 File Offset: 0x000174F8
        public static Type? GenericImplementsType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            this Type type, Type ofType)
        {
            foreach (Type type2 in type.GetInterfaces())
            {
                if (type2.IsGenericType)
                {
                    if (type2.GetGenericTypeDefinition() == ofType)
                    {
                        return type2;
                    }
                }
            }
            return null;
        }

    }

}
