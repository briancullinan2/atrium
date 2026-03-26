using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace DataLayer.Utilities
{
    public class AsyncQueryable<T>(IQueryProvider provider, Expression expression) : IQueryable<T>, IOrderedQueryable<T>, IAsyncEnumerable<T>, IQueryable
    {
        public Expression Expression { get; } = expression;
        public IQueryProvider Provider { get; } = provider;
        public Type ElementType => typeof(T);

        #region Standard LINQ Infrastructure
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Use async enumerator.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            // This maps to AsAsyncEnumerable() logic
            var task = ((IAsyncQueryProvider)Provider).ExecuteAsync<IAsyncEnumerable<T>>(Expression, cancellationToken);
            await foreach (var item in task.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        #endregion

        #region EF Core Async Terminal Maps
        // These replace the need for "using Microsoft.EntityFrameworkCore" at the call site

        public Task<List<T>> ToListAsync(CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.ToListAsync(this, ct);

        public Task<T[]> ToArrayAsync(CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.ToArrayAsync(this, ct);

        public Task<bool> AnyAsync(CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.AnyAsync(this, ct);

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.AnyAsync(this, predicate, ct);

        public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(this, ct);

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(this, predicate, ct);

        public Task<int> CountAsync(CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.CountAsync(this, ct);

        public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.CountAsync(this, predicate, ct);

#endregion

        public IQueryable<TEntity> AsNoTracking<TEntity>()
            where TEntity : class, T
            => EntityFrameworkQueryableExtensions.AsNoTracking<TEntity>(this.OfType<TEntity>());

        public IQueryable<T> AsNoTrackingWithIdentityResolution<TEntity>()
            where TEntity : class, T
            => EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution(this.OfType<TEntity>());

        public IQueryable<T> IgnoreQueryFilters<TEntity>()
            where TEntity : class, T
            => EntityFrameworkQueryableExtensions.IgnoreQueryFilters(this.OfType<TEntity>());

        public IQueryable<T> TagWith(string tag)
            => EntityFrameworkQueryableExtensions.TagWith(this, tag);

        public IIncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> navigationPropertyPath)
            where TEntity : class, T
            => EntityFrameworkQueryableExtensions.Include(this.OfType<TEntity>(), navigationPropertyPath);

        // String-based include for dynamic scenarios
        public IQueryable<T> Include<TEntity>(string navigationPropertyPath)
            where TEntity : class, T
            => EntityFrameworkQueryableExtensions.Include(this.OfType<TEntity>(), navigationPropertyPath);

        public Task<int> ExecuteDeleteAsync(CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(this, ct);

        public Task<int> ExecuteUpdateAsync(Action<UpdateSettersBuilder<T>> setPropertyCalls, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(this, setPropertyCalls, ct);

        public Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.MaxAsync(this, selector, ct);

        public Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.MinAsync(this, selector, ct);

        public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
            => EntityFrameworkQueryableExtensions.SumAsync(this, selector, ct);

        public string ToQueryString()
            => EntityFrameworkQueryableExtensions.ToQueryString(this);

    }
}
