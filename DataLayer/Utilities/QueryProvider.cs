using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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
            var fakeRoot = new List<TEntity>().AsQueryable();
            if (expression is LambdaExpression lambda)
            {
                var visitor = new ParameterUpdateVisitor(lambda.Parameters[0], fakeRoot.Expression);
                var invokedExpression = visitor.Visit(lambda.Body);
                return new AsyncQueryable<TEntity>(this, invokedExpression);
            }
            else
            {
                var swapper = new RootReplacementVisitor(fakeRoot);
                var sqliteExpression = swapper.Visit(expression);
                return new AsyncQueryable<TEntity>(this, sqliteExpression);
            }
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return (IQueryable<TElement>)CreateQuery(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken token)
        {
            // evaluate Task<> types
            var resultType = typeof(TResult);
            var isTask = resultType.IsCompatibleWith(typeof(Task));
            var innerType = resultType.GetGenericArguments()
                .FirstOrDefault()
                ?? typeof(TResult);
            var QueryNow = typeof(QueryManager).GetMethods(nameof(QueryManager.QueryNow), 2, [typeof(StorageType), typeof(Expression)])
                .FirstOrDefault()
                ?.MakeGenericMethod(typeof(TEntity), innerType)
                ?? throw new InvalidOperationException("Could not render QueryNow method");

            return (dynamic)Query.QueryNow<TEntity, TResult>(_storage, expression, _priority);
        }


        // BLOCKADE: This prevents anyone from using .ToList() or .First()
        public object Execute(Expression expression) => throw new InvalidOperationException("Sync queries are forbidden in StudySauce. Use the Async equivalents (e.g. ToListAsync).");
        public TResult Execute<TResult>(Expression expression) => throw new InvalidOperationException("Sync queries are forbidden in StudySauce. Use the Async equivalents.");
    }
}
