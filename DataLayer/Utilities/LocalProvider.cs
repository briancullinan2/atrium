using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DataLayer.Utilities
{
    public class LocalQueryProvider(ICurrentDbContext Current) : IAsyncQueryProvider
    {

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments().FirstOrDefault()
                ?? throw new InvalidOperationException("Could not extract generic arguments.");

            return (IQueryable)Activator.CreateInstance(
                typeof(AsyncQueryable<>).MakeGenericType(elementType),
                [this, expression]
            )!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new AsyncQueryable<TElement>(this, expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var typeT = typeof(TResult);

            if (typeof(Task).IsAssignableFrom(typeT))
            {
                // Get the 'Setting' out of 'Task<Setting>'
                var innerType = typeT.GetGenericArguments().FirstOrDefault() ?? typeof(object);

                // USE REFLECTION to call the inner method so T is 'Setting', not 'Task<Setting>'
                var method = typeof(LocalQueryProvider)
                    .GetMethod(nameof(ExecuteRemoteAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(innerType)
                    ?? throw new InvalidOperationException("Method not found");

                // This now returns Task<Setting>, which IS TResult
                var task = method.Invoke(this, [expression, cancellationToken]);

                return (TResult)task!;
            }

            // Handle IAsyncEnumerable (The 'BS' converter)
            if (typeT.IsGenericType && typeT.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = typeT.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);

                var method = typeof(LocalQueryProvider)
                    .GetMethod(nameof(ExecuteRemoteAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(listType)
                    ?? throw new InvalidOperationException("Method not found");

                var task = method.Invoke(this, [expression, cancellationToken]);
                return (TResult)CreateAsyncEnumerableFromTask(task!, itemType);
            }

            throw new InvalidOperationException($"Unsupported type: {typeT}");
        }

        private object CreateAsyncEnumerableFromTask(object task, Type itemType)
        {
            var method = typeof(LocalQueryProvider)
                .GetMethod(nameof(ToAsyncEnumerableInternal), BindingFlags.NonPublic | BindingFlags.Static)?
                .MakeGenericMethod(itemType)
                ?? throw new InvalidOperationException("Could not render ToAsyncEnumerableInternal function.");

            return method.Invoke(null, [task])!;
        }

        private static async IAsyncEnumerable<T> ToAsyncEnumerableInternal<T>(Task<List<T>?> task)
        {
            var list = await task;
            foreach (var item in list ?? [])
            {
                yield return item;
            }
        }


        private async Task<T> ExecuteRemoteAsync<T>(Expression query, CancellationToken? ct = null)
        {
            var Context = Current.Context as TranslationContext
                ?? throw new InvalidOperationException("Could not render remote storage context.");
            //if (Context.Client == null) throw new InvalidOperationException("No Http client.");

            Console.WriteLine("Executing: " + query.ToString());
            var cleanExpression = new ClosureEvaluatorVisitor().Visit(query);
            var simpleExpression = new AggressiveVisitor().Visit(cleanExpression);

            // This is exactly where you use your Expression Tree Converter
            var serialized = query.ToXDocument().ToString();
            Console.WriteLine("Converted: " + cleanExpression);




            return default!;
#if false


            var finalExpression = query;

            Console.WriteLine("Server responded: " + JsonSerializer.Serialize(finalExpression.Value));
            Console.WriteLine("What fucking part is failing?" + query.IsDefault() + " - " + finalExpression.Value.IsEmpty());
            if (query.IsDefault()
                && (finalExpression.Value == null
                || finalExpression.Value.IsEmpty()))
            {
                return default!;
            }

            // 4. Handle Collections vs Scalars
            if (typeof(T).IsIterable() && !query.IsDefault())
            {
                return (T)CollectionConverter.ConvertAsync(finalExpression.Value, typeof(T))!;
            }
            return (T)finalExpression.Value!;
#endif
        }

        // --- Blockade Sync Methods ---
        public object Execute(Expression expression) => throw new InvalidOperationException("Sync queries forbidden.");
        public TResult Execute<TResult>(Expression expression) => throw new InvalidOperationException("Sync queries forbidden.");

        private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
