using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Extensions.PrometheusTypes
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
            if (source == null)
            {
                //Console.WriteLine("You entered a null: " + source?.GetType() + " - " + source?.ToString());
                return GetDefault(targetType);
            }

            var sourceType = source.GetType();
            if (sourceType.Extends(targetType))
            {
                //Console.WriteLine("Already extends this bastard: " + source?.GetType() + " - " + source?.ToString());
                return source;
            }

            // 1. Get the Inner Type (T)
            var itemType = targetType.IsArray
                ? targetType.GetElementType() ?? typeof(object)
                : (targetType.GetGenericArguments().FirstOrDefault() ?? typeof(object));

            // 2. Ensure source is at least an Enumerable we can work with
            if (source is not IEnumerable enumerableSource)
            {
                //Console.WriteLine("Not even a goddamn enumerable: " + source?.GetType() + " - " + source?.ToString());
                return source;
            }

            // 3. The Conversion Switchboard
            if (targetType.IsGenericType && (
                targetType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                targetType.GetGenericTypeDefinition() == typeof(IList<>) ||
                targetType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                //Console.WriteLine("Converting to plain list: " + source?.GetType() + " - " + source?.ToString());
                var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!
                    .MakeGenericMethod(itemType);

                var toListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!
                    .MakeGenericMethod(itemType);

                // 2. Perform the double-hop
                var castedSource = castMethod.Invoke(null, [source]); // Turns object[] into IEnumerable<Visit>
                return toListMethod.Invoke(null, [castedSource]);
            }


            // CASE: Target is IAsyncEnumerable<T>
            if (IsGenericType(targetType, typeof(IAsyncEnumerable<>)))
            {
                //Console.WriteLine("Making special async list: " + source?.GetType() + " - " + source?.ToString());
                return ToAsyncMethod.MakeGenericMethod(itemType).Invoke(null, [source]);
            }

            // CASE: Target is IQueryable<T>
            if (IsGenericType(targetType, typeof(IQueryable<>)))
            {
                //Console.WriteLine("Making a concrete list out of a queryable: " + source?.GetType() + " - " + source?.ToString());
                var list = EnsureConcreteList(enumerableSource, itemType);
                return Queryable.AsQueryable((IEnumerable)list);
            }

            // CASE: Target is List<T>
            if (IsGenericType(targetType, typeof(List<>)))
            {
                //Console.WriteLine("Making a concrete list: " + source?.GetType() + " - " + source?.ToString());
                return EnsureConcreteList(enumerableSource, itemType);
            }

            // CASE: Target is Array T[]
            if (targetType.IsArray)
            {
                //Console.WriteLine("Making a object array: " + source?.GetType() + " - " + source?.ToString());
                return ToArrayMethod.MakeGenericMethod(itemType).Invoke(null, [source]);
            }

            // CASE: Target is Task<List<T>> (For your Async Execute logic)
            if (IsGenericType(targetType, typeof(Task<>)))
            {
                // If the source is already a Queryable/AsyncEnumerable, we need to ToListAsync it
                if (source is IQueryable || IsGenericType(sourceType, typeof(IAsyncEnumerable<>)))
                {
                    //Console.WriteLine("Making a generic task: " + source?.GetType() + " - " + source?.ToString());
                    return ToListAsyncMethod.MakeGenericMethod(itemType).Invoke(null, [source, null]);
                }
            }

            //Console.WriteLine("Did zero fucking conversion: " + source?.GetType() + " - " + source?.ToString());

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
