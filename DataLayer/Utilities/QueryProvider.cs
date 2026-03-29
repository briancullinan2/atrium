using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;


namespace DataLayer.Utilities
{
    public class EnqueuedQueryProvider<TEntity>(IQueryManager Query, StorageType storage, int priority)
            : IAsyncQueryProvider where TEntity : Entity<TEntity>
    {
        private readonly StorageType _storage = storage;
        private readonly int _priority = priority;

        public IQueryable CreateQuery(Expression expression)
        {
            var fakeRoot = new AsyncQueryable<TEntity>(this, Expression.Constant(new AsyncQueryable<TEntity>(this, Expression.Constant(Enumerable.Empty<TEntity>().AsQueryable()))));

            var swapper = new RootReplacementVisitor(fakeRoot);
            var sqliteExpression = swapper.Visit(expression);
            return new AsyncQueryable<TEntity>(this, sqliteExpression!);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return (IQueryable<TElement>)CreateQuery(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken token)
        {
            var QueryGeneric = Query.GetType().GetMethods(nameof(QueryManager.QueryNow), 2, [typeof(StorageType), typeof(Expression)])
                .FirstOrDefault()
                 ?? throw new InvalidOperationException("Could not render QueryNow method");

            // evaluate Task<> types
            var resultType = typeof(TResult);
            var innerType = resultType.GetGenericArguments().FirstOrDefault();
            var isTask = resultType.Extends(typeof(Task));
            if (typeof(Task).Extends(resultType))
            {
                var QueryNow = QueryGeneric.MakeGenericMethod(typeof(TEntity), innerType ?? typeof(TResult));
                return (TResult)QueryNow.Invoke(Query, [_storage, expression, _priority])!;
            }
            else if (typeof(IAsyncEnumerable<>).Extends(resultType)
                && innerType is Type itemType)
            {
                var listType = typeof(List<>).MakeGenericType(itemType);
                var QueryNow = QueryGeneric.MakeGenericMethod(typeof(TEntity), listType);
                var task = QueryNow.Invoke(Query, [_storage, expression, _priority])
                    ?? throw new InvalidOperationException("Could not render QueryNow function.");

                // Convert Task<List<T>> -> IAsyncEnumerable<T>
                return (TResult)CreateAsyncEnumerableFromTask(task, itemType);
            }
            throw new InvalidOperationException("Whatever this is, it isn't supported: " + typeof(TResult));
        }
        // The "BS" converter EF Core wants
        private object CreateAsyncEnumerableFromTask(object task, Type itemType)
        {
            var method = typeof(EnqueuedQueryProvider<TEntity>)
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

        // BLOCKADE: This prevents anyone from using .ToList() or .First()
        public object Execute(Expression expression) => throw new InvalidOperationException("Sync queries are forbidden in StudySauce. Use the Async equivalents (e.g. ToListAsync).");
        public TResult Execute<TResult>(Expression expression) => throw new InvalidOperationException("Sync queries are forbidden in StudySauce. Use the Async equivalents.");
    }
}
