
#if false
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace DataLayer.Utilities
{
#pragma warning disable EF1001 // Internal EF Core API usage.
    public class RemoteQuery : IQueryCompiler
#pragma warning restore EF1001 // Internal EF Core API usage.
    {
        //private readonly ICurrentDbContext _current;
        private readonly HttpClient Client;

        public static MethodInfo ExecuteRemote { get; }

        private RemoteStorage Context { get; set; }
        
        public RemoteQuery(ICurrentDbContext context)
        {
            //_context = context;
            //Console.WriteLine("I hate DI: " + dbContext);
            //Console.WriteLine("I hate DI: " + internalServiceProvider);
            Context = context.Context as RemoteStorage 
                ?? throw new InvalidOperationException("Remote parent context is unexpected type: " + context.Context.GetType() + context.Context.ToString());
            
            Client = Context.Client;
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
                var itemType = typeT.GetGenericArguments().FirstOrDefault()
                    ?? throw new InvalidOperationException("Could not extract generic arugments.");
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
                var innerType = typeT.IsGenericType ? typeT.GetGenericArguments().FirstOrDefault() ?? typeof(object) : typeof(object);

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

                return task.Result;
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


        public static object? GetDefault(Type type)
        {
            // 1. If it's a Value Type (int, DateTime, struct, etc.)
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            // 2. If it's a Reference Type (class, string, interface)
            return null;
        }



        public async Task<T> ExecuteRemoteAsync<T>(Expression query, CancellationToken cancellationToken = default)
        {
            if (Client == null)
            {
                throw new InvalidOperationException("No Http client.");
            }
            //if (Service == null)
            //{
            //    throw new InvalidOperationException("No DbContext provider.");
            //}

            // The key is checking the requested type T
            var typeT = typeof(T);

            Console.WriteLine("Querying serialized: " + query.ToString());
            var serialized = query.ToXDocument().ToString();
            var baseAddress = Context.BaseAddress;
            var queryAddress = (!string.IsNullOrEmpty(baseAddress) ? (baseAddress + (!baseAddress.EndsWith('/') ? '/' : "")) : "")
                + "/api/query";

            var response = await Client.PostAsJsonAsync(queryAddress, serialized, cancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                object? defaultObject;
                if (typeT.IsIterable() && !query.IsDefault())
                {
                    defaultObject = CollectionConverter.ConvertAsync(null, typeT);
                    if (defaultObject is Task task)
                        defaultObject = (task as dynamic).Result;
                }
                else
                {
                    defaultObject = GetDefault(typeT)!;
                }

                return (T)defaultObject!;
            }


            var content = await response.Content.ReadFromJsonAsync<string>(cancellationToken: cancellationToken);

            using XmlReader reader = XmlReader.Create(new StringReader(content ?? string.Empty));
            _ = reader.MoveToContent();
            XElement root = (XElement)XNode.ReadFrom(reader);

            ConstantExpression? finalExpression = root.ToExpression(Context, out IQueryable? set) as ConstantExpression
                ?? throw new InvalidOperationException("Could not convert expression document to Queryable: " + query);



            // If the caller (EF Core) is asking for IAsyncEnumerable<File>
            if (typeT.IsGenericType && typeT.IsIterable())
            {
                var itemType = typeT.GetGenericArguments().FirstOrDefault()
                    ?? throw new InvalidOperationException("Could not extract generic arugments.");
                var listType = typeof(List<>).MakeGenericType(itemType);

                // 1. Deserialize as a concrete List first
                if (finalExpression.Value == null && Nullable.GetUnderlyingType(typeof(T)) == null)
                {
                    throw new InvalidOperationException("Server returned null and nobody knows why.");
                }

                return (T)CollectionConverter.ConvertAsync(finalExpression.Value, typeT)!;
            }

            //var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            if (finalExpression.Value != null && finalExpression.Value?.GetType() != typeT)
            {
                Console.WriteLine("Invalid cast: " + finalExpression.Value?.GetType() + " to " + typeT);
            }
            return (T)finalExpression.Value!;
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
#endif

