using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                    .GetMethod(nameof(ExecuteIDBAsync), BindingFlags.NonPublic | BindingFlags.Instance)
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
                    .GetMethod(nameof(ExecuteIDBAsync), BindingFlags.NonPublic | BindingFlags.Instance)
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



        public static MethodInfo? ToForced { get; }
        public static MethodInfo? QueryIndexAsync { get; }

        internal static List<List<(MemberInfo Member, ExpressionType Type, object? Value)>> BucketLogicalComparators(
            List<(MemberInfo Member, ExpressionType Type, object? Value)> Comparators,
            List<ExpressionType> Logical
        ) {

            // 1. Bucket by OrElse (Split the comparators into independent lists)
            var buckets = new List<List<(MemberInfo Member, ExpressionType Type, object? Value)>> { new() };
            for (int i = 0; i < Comparators.Count; i++)
            {
                buckets.Last().Add(Comparators[i]);
                if (i < Logical.Count && Logical[i] == ExpressionType.OrElse)
                    buckets.Add([]);
            }

            return buckets;
        }


        internal static (string Index, object? Exact, object? Lower, object? Upper, bool IsSpecific) GenerateQueryPlan(
            List<(MemberInfo Member, ExpressionType Type, object? Value)> bucket
        )
        {
            var grouped = bucket.GroupBy(c => c.Member.Name);
            var projected = grouped.Select(g => {
                var exact = g.FirstOrDefault(c => c.Type == ExpressionType.Equal).Value;
                var lower = g.FirstOrDefault(c => c.Type == ExpressionType.GreaterThanOrEqual || c.Type == ExpressionType.GreaterThan).Value;
                var upper = g.FirstOrDefault(c => c.Type == ExpressionType.LessThanOrEqual || c.Type == ExpressionType.LessThan).Value;

                // Priority: Exact Match > Bounded Range > Single Bound
                return (
                    Index: g.Key.ToCamelCase(), /* must match indexes in schema TestStorage.PerformInitialization */
                    Exact: exact,
                    Lower: lower,
                    Upper: upper,
                    IsSpecific: exact != null || (lower != null && upper != null)
                );
            });

            // Pick the most restrictive index in this bucket to hit IDB with
            return projected.OrderByDescending(x => x.IsSpecific).First();
        }

        static LocalQueryProvider()
        {
            ToForced = typeof(QueryableExtensions).GetMethods(nameof(QueryableExtensions.ToForced))
                        .FirstOrDefault();
            QueryIndexAsync = typeof(LocalStore).GetMethods(nameof(LocalStore.QueryIndexAsync))
                        .FirstOrDefault();
        }


        private async Task<T> ExecuteIDBAsync<T>(Expression query, CancellationToken? ct = null)
        {
            var Context = Current.Context as TestStorage
                ?? throw new InvalidOperationException("Could not render remote storage context.");
            //if (Context.Client == null) throw new InvalidOperationException("No Http client.");

            if (Context.Store == null)
                throw new InvalidOperationException("IDB Module not setup for query.");

            Console.WriteLine("Executing: " + query.ToString());

            MethodCallExpression simpleExpression;
            AggressiveVisitor visitor;
            try
            {

                var rootSwapped = new RootReplacementVisitor(null).Visit(query);
                var cleanExpression = new ClosureEvaluatorVisitor().Visit(rootSwapped);
                visitor = new AggressiveVisitor();
                simpleExpression = visitor.Visit(cleanExpression) as MethodCallExpression
                    ?? throw new InvalidOperationException("Could render a clean IDB compatible expression.");
                var values = visitor.Recordings.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
                Console.WriteLine(JsonSerializer.Serialize(values));

                // This is exactly where you use your Expression Tree Converter
                var serialized = query.ToXDocument().ToString();
                Console.WriteLine("Converted: " + simpleExpression);


            }
            catch (Exception ex)
            {
                Console.WriteLine("Query will fail: " + query.ToString() + " - " + ex);
                // throw up here because we want to know when our statement is bad
                throw new InvalidOperationException("Query will fail: " + query.ToString() + " - " + ex);
            }


            try
            {
                // can't do much to optimize here
                var database = visitor.Recordings.Keys.OrderBy(Selectors.OrderDatabaseQueries).ToList();
                foreach (var method in database)
                {
                    var recording = visitor.Recordings[method];
                    var tableName = recording.EntityType?.Table()
                        ?? throw new InvalidOperationException("Could not extract table name from expression.");

                    System.Collections.IList results = Activator.CreateInstance(typeof(List<>)
                        .MakeGenericType(recording.EntityType)) as System.Collections.IList
                        ?? throw new InvalidOperationException("Could not render collection container");

                    List<List<object?>> predicatedObjs = [];
                    var predicate = recording.EntityType.Predicate();
                    var bounded = BucketLogicalComparators(recording.Comparators, recording.Logical);
                    // 2. Process each bucket into the best possible Index Range
                    foreach (var bucket in bounded)
                    {
                        var plan = GenerateQueryPlan(bucket);
                        var queryMethod = QueryIndexAsync?.MakeGenericMethod(recording.EntityType)
                            ?? throw new InvalidOperationException("Could not render QueryIndexAsync method.");
                        var newSet = queryMethod.Invoke(Context.Store, [tableName, plan.Index.ToCamelCase() /* must match schema */ , plan.Exact, plan.Lower, plan.Upper]);
                        if(newSet?.GetType().Extends(typeof(ValueTask<>)) == true
                            && (newSet as dynamic).AsTask() is Task task)
                        {
                            await task;
                            newSet = (newSet as dynamic).Result;
                        }

                        if(newSet is IList list)
                        foreach(var item in list)
                        {
                            var predicateValues = predicate.Select(p => p.GetValue(item)).ToList();
                            if (predicatedObjs.Contains(predicateValues))
                                continue;
                            predicatedObjs.Add(predicateValues);
                            results.Add(item);
                        }
                    }

                    // TODO: extract and check predicate from entity type
                    // TODO: swap out set provider for actual data reevaluate query just like in QueryNow()
                    var finalQuery = results.AsQueryable();
                    var finalSwapped = new EnumerableSwitcher(recording.EntityType, finalQuery.Expression).Visit(simpleExpression)
                        ?? throw new InvalidOperationException("Could not render final data expression." 
                            + simpleExpression + " for " + results.ToString() + " " + results.GetType().FullName);
                    
                    //var asQueryableMethod = typeof(Queryable).GetMethods()
                    //    .First(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethod)
                    //    .MakeGenericMethod(recording.EntityType);

                    //finalSwapped = Expression.Call(null, asQueryableMethod, finalSwapped);

                    //if (typeof(T).Extends(typeof(IEnumerable))
                    //    && !simpleExpression.IsProjection()
                    //    && !simpleExpression.IsTerminal())
                    {
                        var lambda = Expression.Lambda(finalSwapped);
                        var compiled = lambda.Compile();
                        var finalResult = compiled.DynamicInvoke();
                        //var forcedMethod = ToForced?.MakeGenericMethod(recording.EntityType, finalResult.ElementType)
                        //    ?? throw new InvalidOperationException("Could not render ToForced method.");
                        if(typeof(T).Extends(typeof(IEnumerable))
                            && (!simpleExpression.IsDefault() 
                                || finalResult is IEnumerable enumerable
                                && !enumerable.IsEmpty())) // return null
                        {
                            finalResult = CollectionConverter.ConvertAsync(finalResult, typeof(T));
                        }
                        return (T)finalResult; //ToForced.Invoke(null, [finalResult])!;
                    }
                    //else
                    //{
                    //    var result = finalQuery.Provider.Execute(finalSwapped) as dynamic;
                    //    if (result is T) return result;
                    //    return default!;
                    //}
                }

                // TODO: reconstruct SelectMany queries


            }
            catch (Exception ex)
            {
                Console.WriteLine("Lookup failed: " + query.ToString() + " - " + ex);
                // don't throw up because we don't want to gag our UX
            }

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
