using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Reflection;

namespace DataLayer.Utilities
{
#pragma warning disable EF1001 // Internal EF Core API usage.
    public class RemoteQuery : IQueryCompiler
#pragma warning restore EF1001 // Internal EF Core API usage.
    {
        private readonly HttpClient? _httpClient;
        public static IServiceProvider? Service { get; set; } = null;
        public static MethodInfo ExecuteRemote { get; }

        public RemoteQuery()
        {
            _httpClient = Service?.GetRequiredService<HttpClient>();
        }



        public TResult Execute<TResult>(Expression query)
        {
            throw new InvalidOperationException("Synchronous not supported, evaluate then use ToQueryable to convert back.");
            /*
            if (_httpClient == null)
            {
                throw new InvalidOperationException("No Http client.");
            }

            Console.WriteLine("Executing: " + query.ToString());
            // This is exactly where you use your Expression Tree Converter
            var serialized = query.ToXDocument().ToString();
            Console.WriteLine("Converted: " + query.ToString());

            // Send to your remote endpoint
            var response = _httpClient.PostAsJsonAsync("/api/query", serialized).Result;

            return response.Content.ReadFromJsonAsync<TResult>().Result!;
            */
        }

        static RemoteQuery()
        {
            ExecuteRemote = typeof(RemoteQuery)
                    .GetMethod(nameof(ExecuteRemoteAsync),
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Failed to find ExecuteRemoteAsync");
        }


        // You must also implement ExecuteAsync for ToListAsync() support
        public TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken = default)
        {
            var typeT = typeof(TResult);

            // 1. Handle IAsyncEnumerable (same as your current logic)
            if (typeT.IsGenericType && typeT.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = typeT.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);

                var task = ExecuteRemote.MakeGenericMethod(listType)
                    .Invoke(this, [query, cancellationToken]) as Task
                    ?? throw new InvalidOperationException("Failed to invoke ExecuteRemoteAsync");

                return (TResult)CreateAsyncEnumerableFromTask(task, itemType);
            }

            // 2. Handle Tasks (Scalars like CountAsync, ToListAsync, etc.)
            if (typeof(Task).IsAssignableFrom(typeT))
            {
                // Extract the 'int' from 'Task<int>'
                var innerType = typeT.IsGenericType ? typeT.GetGenericArguments()[0] : typeof(object);

                // Call ExecuteRemoteAsync<int>(...)
                var task = ExecuteRemote.MakeGenericMethod(innerType)
                    .Invoke(this, [query, cancellationToken])
                    ?? throw new InvalidOperationException("Failed to create object query task: " + typeT);

                // 'task' is already the Task<int> EF Core wants. 
                // We just cast it to TResult (which is Task<int>) and return.
                return (TResult)task!;
            }
            else
            {
                var task = ExecuteRemote.MakeGenericMethod(typeT)
                    .Invoke(this, [query, cancellationToken]) as Task<TResult>
                    ?? throw new InvalidOperationException("Failed to create TResult query task: " + typeT);

                return (TResult)task.Result!;
            }
        }

        // Helper to wrap the Task into a stream EF Core can read
        private object CreateAsyncEnumerableFromTask(Task task, Type itemType)
        {
            var method = (typeof(RemoteQuery)
                .GetMethod(nameof(ToAsyncEnumerableInternal),
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.MakeGenericMethod(itemType))
                ?? throw new InvalidOperationException("Couldn't resolve method type.");
            return method.Invoke(this, [task])!;
        }

        private static async IAsyncEnumerable<T> ToAsyncEnumerableInternal<T>(Task task)
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
            if (_httpClient == null)
            {
                throw new InvalidOperationException("No Http client.");
            }

            Log.Debug("Querying serialized: " + query.ToString());
            var serialized = query.ToXDocument().ToString();
            var response = await _httpClient.PostAsJsonAsync("api/query", serialized, cancellationToken);
            _ = response.EnsureSuccessStatusCode();

            // The key is checking the requested type T
            var typeT = typeof(T);

            // If the caller (EF Core) is asking for IAsyncEnumerable<File>
            if (typeT.IsGenericType && typeof(IAsyncEnumerable<>).IsAssignableFrom(typeT.GetGenericTypeDefinition()))
            {
                var itemType = typeT.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);

                // 1. Deserialize as a concrete List first
                var list = await response.Content.ReadFromJsonAsync(listType, cancellationToken: cancellationToken);
                if (list == null && Nullable.GetUnderlyingType(typeof(T)) == null)
                {
                    throw new InvalidOperationException("Server returned null and nobody knows why.");
                }

                // 2. Convert List to IAsyncEnumerable (using .ToAsyncEnumerable())
                // Requires 'System.Linq.Async' NuGet package
                var toAsyncMethod = typeof(AsyncEnumerable)
                    .GetMethods()
                    .First(m => m.Name == nameof(AsyncEnumerable.ToAsyncEnumerable) && m.IsGenericMethod)
                    .MakeGenericMethod(itemType);

                var result2 = toAsyncMethod.Invoke(null, [list]);
                return (T)result2!;
            }

            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return result!;
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
