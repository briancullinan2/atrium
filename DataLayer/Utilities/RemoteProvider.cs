using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

namespace DataLayer.Utilities
{
    public class RemoteQueryProvider(ICurrentDbContext Current) : IAsyncQueryProvider
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
                var method = typeof(RemoteQueryProvider)
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

                var method = typeof(RemoteQueryProvider)
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
            var method = typeof(RemoteQueryProvider)
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
            var Context = Current.Context as RemoteStorage
                ?? throw new InvalidOperationException("Could not render remote storage context.");
            if (Context.Client == null) throw new InvalidOperationException("No Http client.");

            Console.WriteLine("Executing: " + query.ToString());
            var cleanExpression = new ClosureEvaluatorVisitor().Visit(query);

            // This is exactly where you use your Expression Tree Converter
            var serialized = query.ToXDocument().ToString();
            Console.WriteLine("Converted: " + cleanExpression);
            //var compressed = XNodeTruncator.Truncate(doc).ToString();
            //var tinyPayload = ExpressionMinifier.Minify(cleanExpr);

            // 1. Serialize Expression to XML/XDocument
            var baseAddress = Context.BaseAddress?.TrimEnd('/');
            var queryAddress = (!string.IsNullOrEmpty(baseAddress) ? (baseAddress + (!baseAddress.EndsWith('/') ? '/' : "")) : "")
                + "api/query";

            // 2. Post to Remote Endpoint
            var response = await Context.Client.PostAsJsonAsync(queryAddress, serialized, JsonHelper.Default, ct ?? default);

            if (!response.IsSuccessStatusCode)
            {
                if (typeof(T).IsIterable() && !query.IsDefault())
                {
                    return (T)CollectionConverter.ConvertAsync(null, typeof(T))!;
                }
                return (T)GetDefault(typeof(T))!;
            }



            // 3. Deserialize Result (Simplified version of your RemoteQuery logic)
            var content = await response.Content.ReadFromJsonAsync<string>(JsonHelper.Default, ct ?? default);
            using var reader = new StringReader(content ?? string.Empty);
            var root = XElement.Load(reader);

            var finalExpression = root.ToExpression(Context, out _) as ConstantExpression
                ?? throw new InvalidOperationException("Failed to convert remote result to Expression.");

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
        }

        // --- Blockade Sync Methods ---
        public object Execute(Expression expression) => throw new InvalidOperationException("Sync queries forbidden.");
        public TResult Execute<TResult>(Expression expression) => throw new InvalidOperationException("Sync queries forbidden.");

        private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}