using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Xml.Linq;

namespace DataLayer.Utilities
{
    public class RemoteQueryProvider(RemoteStorage context, int priority = 0) : IAsyncQueryProvider
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
            // This replaces QueryNow and IQueryCompiler.ExecuteAsync
            // TResult is usually Task<IEnumerable<T>> or Task<T> (for scalars)
            var typeT = typeof(TResult);

            // Handle Tasks (Scalars like CountAsync, FirstOrDefaultAsync, or ToListAsync)
            if (typeof(Task).IsAssignableFrom(typeT))
            {
                var innerType = typeT.IsGenericType
                    ? typeT.GetGenericArguments().FirstOrDefault() ?? typeof(object)
                    : typeof(object);

                // This triggers the actual remote network call
                var task = ExecuteRemoteAsync<TResult>(innerType, expression, cancellationToken);

                // Return the Task directly to EF Core
                return (TResult)(object)task;
            }

            throw new InvalidOperationException("Only Async terminal methods are supported in StudySauce.");
        }

        private async Task<T> ExecuteRemoteAsync<T>(Type innerType, Expression query, CancellationToken ct)
        {
            if (context.Client == null) throw new InvalidOperationException("No Http client.");

            // 1. Serialize Expression to XML/XDocument
            var serialized = query.ToXDocument().ToString();
            var queryAddress = $"{context.BaseAddress?.TrimEnd('/')}/api/query";

            // 2. Post to Remote Endpoint
            var response = await context.Client.PostAsJsonAsync(queryAddress, serialized, ct);

            if (!response.IsSuccessStatusCode)
            {
                return (T)GetDefault(typeof(T))!;
            }

            // 3. Deserialize Result (Simplified version of your RemoteQuery logic)
            var content = await response.Content.ReadFromJsonAsync<string>(ct);
            using var reader = new StringReader(content ?? string.Empty);
            var root = XElement.Load(reader);

            var finalExpression = root.ToExpression(context, out _) as ConstantExpression
                ?? throw new InvalidOperationException("Failed to convert remote result to Expression.");

            // 4. Handle Collections vs Scalars
            if (typeof(T).IsIterable())
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