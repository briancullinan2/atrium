using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DataLayer.Utilities
{
    public static class CollectionConverter
    {
        private static readonly MethodInfo ToListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!;
        private static readonly MethodInfo ToArrayMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!;
        private static readonly MethodInfo ToAsyncMethod = typeof(AsyncEnumerable).GetMethods()
            .First(m => m.Name == nameof(AsyncEnumerable.ToAsyncEnumerable) && m.IsGenericMethod);
        private static readonly MethodInfo ToListAsyncMethod = typeof(AsyncEnumerable).GetMethods()
            .First(m => m.Name == nameof(AsyncEnumerable.ToListAsync) && m.IsGenericMethod);

        public static object? ConvertAsync(object? source, Type targetType)
        {
            if (source == null) return GetDefault(targetType);

            var sourceType = source.GetType();
            if (targetType.IsAssignableFrom(sourceType)) return source;

            // 1. Get the Inner Type (T)
            var itemType = targetType.IsArray
                ? targetType.GetElementType() ?? typeof(object)
                : (targetType.GetGenericArguments().FirstOrDefault() ?? typeof(object));

            // 2. Ensure source is at least an Enumerable we can work with
            if (source is not IEnumerable enumerableSource) return source;

            // 3. The Conversion Switchboard

            // CASE: Target is IAsyncEnumerable<T>
            if (IsGenericType(targetType, typeof(IAsyncEnumerable<>)))
            {
                return ToAsyncMethod.MakeGenericMethod(itemType).Invoke(null, [source]);
            }

            // CASE: Target is IQueryable<T>
            if (IsGenericType(targetType, typeof(IQueryable<>)))
            {
                var list = EnsureConcreteList(enumerableSource, itemType);
                return Queryable.AsQueryable((IEnumerable)list);
            }

            // CASE: Target is List<T>
            if (IsGenericType(targetType, typeof(List<>)))
            {
                return EnsureConcreteList(enumerableSource, itemType);
            }

            // CASE: Target is Array T[]
            if (targetType.IsArray)
            {
                return ToArrayMethod.MakeGenericMethod(itemType).Invoke(null, [source]);
            }

            // CASE: Target is Task<List<T>> (For your Async Execute logic)
            if (IsGenericType(targetType, typeof(Task<>)))
            {
                // If the source is already a Queryable/AsyncEnumerable, we need to ToListAsync it
                if (source is IQueryable || IsGenericType(sourceType, typeof(IAsyncEnumerable<>)))
                {
                    return ToListAsyncMethod.MakeGenericMethod(itemType).Invoke(null, [source, null]);
                }
            }

            return source;
        }

        private static object EnsureConcreteList(IEnumerable source, Type itemType)
        {
            // Use Enumerable.Cast<T>().ToList() to ensure type safety
            var casted = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!
                .MakeGenericMethod(itemType).Invoke(null, [source]);
            return ToListMethod.MakeGenericMethod(itemType).Invoke(null, [casted])!;
        }

        private static bool IsGenericType(Type type, Type genericDefinition) =>
            type.IsGenericType && (type.GetGenericTypeDefinition() == genericDefinition ||
            type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericDefinition));

        public static object? GetEmptyQueryable(Type entityType)
        {
            // 1. Create an empty list of the specific Type
            var listType = typeof(List<>).MakeGenericType(entityType);
            var list = Activator.CreateInstance(listType);

            // 2. Turn it into a Queryable
            return ConvertAsync(list, entityType);
        }

        private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : GetEmptyQueryable(type);
    }
}
