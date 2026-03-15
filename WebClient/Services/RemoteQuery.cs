using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Linq.Expressions;
using System.Net.Http.Json;

namespace WebClient.Services
{
    public class RemoteQuery : IQueryCompiler
    {
        private readonly HttpClient? _httpClient;
        internal static IServiceProvider? _service;

        public RemoteQuery()
        {
            _httpClient = _service?.GetRequiredService<HttpClient>();
        }


        public TResult Execute<TResult>(Expression query)
        {
            Console.WriteLine("Executing: " + query.ToString());
            // This is exactly where you use your Expression Tree Converter
            var serialized = query.ToXDocument().ToString();
            Console.WriteLine("Converted: " + query.ToString());

            // Send to your remote endpoint
            var response = _httpClient.PostAsJsonAsync("/api/query", serialized).Result;

            return response.Content.ReadFromJsonAsync<TResult>().Result;
        }

        // You must also implement ExecuteAsync for ToListAsync() support
        public TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken = default)
        {
            var typeT = typeof(TResult);

            // If TResult is IAsyncEnumerable<File>, we need to fetch List<File>
            if (typeT.IsGenericType && typeT.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = typeT.GetGenericArguments()[0];

                // Use reflection to call ExecuteRemoteAsync<List<File>>
                var listType = typeof(List<>).MakeGenericType(itemType);
                var task = (Task)typeof(RemoteQuery)
                    .GetMethod(nameof(ExecuteRemoteAsync))
                    .MakeGenericMethod(listType) // THIS IS THE KEY: Ask for the List
                    .Invoke(this, new object[] { query, cancellationToken });

                // Bridge the Task<List<File>> to IAsyncEnumerable<File>
                return (TResult)CreateAsyncEnumerableFromTask(task, itemType);
            }

            // Fallback for scalars (Count, Any, etc.)
            return (TResult)typeof(RemoteQuery)
                .GetMethod(nameof(ExecuteRemoteAsync))
                .MakeGenericMethod(typeT)
                .Invoke(this, new object[] { query, cancellationToken });
        }

        // Helper to wrap the Task into a stream EF Core can read
        private object CreateAsyncEnumerableFromTask(Task task, Type itemType)
        {
            var method = typeof(RemoteQuery)
                .GetMethod(nameof(ToAsyncEnumerableInternal), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .MakeGenericMethod(itemType);

            return method.Invoke(this, new object[] { task });
        }

        private async IAsyncEnumerable<T> ToAsyncEnumerableInternal<T>(Task task)
        {
            await task; // Wait for the network call to finish
            var result = (IEnumerable<T>)((dynamic)task).Result;
            foreach (var item in result)
            {
                yield return item;
            }
        }

        public async Task<T> ExecuteRemoteAsync<T>(Expression query, CancellationToken cancellationToken = default)
        {
            var serialized = query.ToXDocument().ToString();
            var response = await _httpClient.PostAsJsonAsync("api/query", serialized, cancellationToken);
            response.EnsureSuccessStatusCode();

            // The key is checking the requested type T
            var typeT = typeof(T);

            // If the caller (EF Core) is asking for IAsyncEnumerable<File>
            if (typeT.IsGenericType && typeT.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = typeT.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);

                // 1. Deserialize as a concrete List first
                var list = await response.Content.ReadFromJsonAsync(listType, cancellationToken: cancellationToken);

                // 2. Convert List to IAsyncEnumerable (using .ToAsyncEnumerable())
                // Requires 'System.Linq.Async' NuGet package
                var toAsyncMethod = typeof(AsyncEnumerable)
                    .GetMethods()
                    .First(m => m.Name == "ToAsyncEnumerable" && m.IsGenericMethod)
                    .MakeGenericMethod(itemType);

                return (T)toAsyncMethod.Invoke(null, new[] { list });
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }

        public Func<QueryContext, TResult> CreateCompiledAsyncQuery<TResult>(Expression query)
        {
            return (queryContext) => this.ExecuteAsync<TResult>(query, queryContext.CancellationToken);
        }

        public Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query)
        {
            return (queryContext) => this.Execute<TResult>(query);
        }
        public Expression<Func<QueryContext, TResult>> PrecompileQuery<TResult>(Expression query, bool async)
        {
            if (async)
            {
                return (queryContext) => this.ExecuteAsync<TResult>(query, queryContext.CancellationToken);
            }

            return (queryContext) => this.Execute<TResult>(query);
        }
    }
}
