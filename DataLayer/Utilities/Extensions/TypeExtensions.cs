using System.Collections;

namespace DataLayer.Utilities.Extensions
{
    // Token: 0x0200005E RID: 94
    public static class TypeExtensions
    {
        // Token: 0x060002F6 RID: 758 RVA: 0x00019200 File Offset: 0x00017400
        public static bool IsLocked(this object o)
        {
            bool result;
            if (!Monitor.TryEnter(o))
            {
                result = true;
            }
            else
            {
                Monitor.Exit(o);
                result = false;
            }
            return result;
        }

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

        // Token: 0x060002F8 RID: 760 RVA: 0x00019278 File Offset: 0x00017478
        public static Type? GenericExtendsType(this Type type, Type ofType)
        {
            foreach (Type type2 in type.GetBaseTypes())
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


        private static readonly Type[] IterableBaseTypes =
        [
            typeof(IEnumerable<>),
            typeof(IAsyncEnumerable<>),
            typeof(IEnumerable) // Catch-all for non-generic collections like ArrayList
        ];

        public static bool IsIterable(this Type type)
        {
            // 1. Strings are technically char sequences, but we treat them as atoms
            if (type == typeof(string)) return false;

            // 2. Arrays are always iterable
            if (type.IsArray) return true;

            // 3. Check the Type itself if it's an open or closed generic of our bases
            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (IterableBaseTypes.Any(baseType => baseType == def)) return true;
            }

            // 4. Check the interface hierarchy
            return type.GetInterfaces().Any(i =>
                i == typeof(IEnumerable) ||
                (i.IsGenericType && IterableBaseTypes.Contains(i.GetGenericTypeDefinition()))
            );
        }


        public static bool IsCompatibleWith(this Type type, Type genericDefinition)
        {
            // 1. Is the type itself the generic definition (e.g., IEnumerable<T> == IEnumerable<>)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
                return true;

            // 2. Does it implement the interface (e.g., List<T> implements IEnumerable<T>)
            return type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == genericDefinition);
        }

        // Token: 0x060002F9 RID: 761 RVA: 0x000192F8 File Offset: 0x000174F8
        public static Type? GenericImplementsType(this Type type, Type ofType)
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

        public static string Limit(this string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0)
                return string.Empty;

            if (value.Length <= maxLength)
                return value;

            // If the field is too small for "...", just hard truncate
            if (maxLength <= 3)
                return value[..maxLength];

            // Standard truncation with suffix
            return value[..(Math.Min(value.Length, maxLength) - 3)] + "...";
        }

        public static bool IsSimple(this Type type)
        {
            return type.IsPrimitive ||
                type.IsEnum ||
                new[] {
            typeof(string),
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid)
                }.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) 
                    && type.GetGenericArguments().FirstOrDefault()?.IsSimple() == true);
        }
    }

}
