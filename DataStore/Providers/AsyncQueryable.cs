namespace DataStore.Providers;

public class AsyncQueryable<TSource>(IQueryProvider provider, Expression expression)
    : DynamicObject, IAsyncQueryable<TSource>
{
    public Expression Expression { get; } = expression;
    public IQueryProvider Provider { get; } = provider;
    public Type ElementType => typeof(TSource);

    #region Blocked Synchronous Leaf Methods
    // Specifically blocking the standard IEnumerable leaf calls
    public IEnumerator<TSource> GetEnumerator() => throw new NotSupportedException("Synchronous enumeration is not supported. Use ToListAsync or await foreach.");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion

    public async IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var task = ((IAsyncQueryProvider)Provider).ExecuteAsync<IAsyncEnumerable<TSource>>(Expression, cancellationToken);
        await foreach (var item in task.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {

        // 1. Check if it's an EF/Async extension we care about
        var asyncEquivalentName = $"{binder.Name}Async";
        var hasAsyncCounterpart = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(m => m.Name == asyncEquivalentName);

        if (hasAsyncCounterpart)
        {
            // This is a terminal leaf like ToList, Any, or Count. 
            // A.R.S. § 44-7007 requires we force the async path for reliability.
            throw new InvalidOperationException(
                $"Blocking synchronous '{binder.Name}'. Use '{asyncEquivalentName}' to maintain " +
                "thread reliability in the AtriumCache local context.");
        }


        // 1. Prepare the arguments (Extension methods take 'this' as the first arg)
        object?[] extendedArgs = [];
        if (args != null)
        {
            extendedArgs = new object[args.Length + 1];
            extendedArgs[0] = this;
            Array.Copy(args, 0, extendedArgs, 1, args.Length);
        }

        // 2. Find the method in EntityFrameworkQueryableExtensions
        // We look for a method matching the name and argument count
        var method = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == binder.Name)
            .FirstOrDefault(m => m.GetParameters().Length == extendedArgs.Length);

        if (method != null)
        {
            // 3. Handle Generic Methods (like Include<T, TProperty> or ToListAsync<TSource>)
            if (method.IsGenericMethod)
            {
                // Most EF extensions use the element type as the first generic argument
                // We can infer them from the arguments or default to typeof(T)
                var genericArgs = method.GetGenericArguments();
                var typeArguments = new Type[genericArgs.Length];
                for (int i = 0; i < genericArgs.Length; i++) typeArguments[i] = typeof(TSource);

                method = method.MakeGenericMethod(typeArguments);
            }

            result = method.Invoke(null, extendedArgs);
            return true;
        }

        return base.TryInvokeMember(binder, args, out result);
    }

    public string ToQueryString(IQueryable source)
        => EntityFrameworkQueryableExtensions.ToQueryString(source);

    #region Any/All
    public Task<bool> AnyAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.AnyAsync(this, token);
    public Task<bool> AnyAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.AnyAsync(this, predicate, token);
    public Task<bool> AllAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.AllAsync(this, predicate, token);
    #endregion

    #region Count/LongCount
    public Task<int> CountAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.CountAsync(this, token);
    public Task<int> CountAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.CountAsync(this, predicate, token);
    public Task<long> LongCountAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.LongCountAsync(this, token);
    public Task<long> LongCountAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.LongCountAsync(this, predicate, token);
    #endregion

    #region ElementAt
    public Task<TSource> ElementAtAsync(int index, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.ElementAtAsync(this, index, token);
    public Task<TSource?> ElementAtOrDefaultAsync(int index, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.ElementAtOrDefaultAsync(this, index, token);
    #endregion

    #region First/FirstOrDefault
    public Task<TSource> FirstAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.FirstAsync(this, token);
    public Task<TSource> FirstAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.FirstAsync(this, predicate, token);
    public Task<TSource?> FirstOrDefaultAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(this, token);
    public Task<TSource?> FirstOrDefaultAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(this, predicate, token);
    #endregion

    #region Last/LastOrDefault
    public Task<TSource> LastAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.LastAsync(this, token);
    public Task<TSource> LastAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.LastAsync(this, predicate, token);
    public Task<TSource?> LastOrDefaultAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.LastOrDefaultAsync(this, token);
    public Task<TSource?> LastOrDefaultAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.LastOrDefaultAsync(this, predicate, token);
    #endregion

    #region Single/SingleOrDefault
    public Task<TSource> SingleAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.SingleAsync(this, token);
    public Task<TSource> SingleAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.SingleAsync(this, predicate, token);
    public Task<TSource?> SingleOrDefaultAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(this, token);
    public Task<TSource?> SingleOrDefaultAsync(Expression<Func<TSource, bool>> predicate, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(this, predicate, token);
    #endregion

    #region Min/Max/Sum/Average
    public Task<TSource> MinAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.MinAsync(this, token);
    public Task<TResult> MinAsync<TResult>(Expression<Func<TSource, TResult>> selector, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.MinAsync(this, selector, token);
    public Task<TSource> MaxAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.MaxAsync(this, token);
    public Task<TResult> MaxAsync<TResult>(Expression<Func<TSource, TResult>> selector, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.MaxAsync(this, selector, token);

    public Task<decimal> SumAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.SumAsync(this.Cast<decimal>(), token);
    public Task<decimal> SumAsync(Expression<Func<TSource, decimal>> selector, CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.SumAsync(this, selector, token);

    public Task<decimal> AverageAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.AverageAsync(this.Cast<decimal>(), token);
    #endregion

    #region Collections
    public Task<List<TSource>> ToListAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.ToListAsync(this, token);
    public Task<TSource[]> ToArrayAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.ToArrayAsync(this, token);
    public Task<HashSet<TSource>> ToHashSetAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.ToHashSetAsync(this, token);
    #endregion

    #region EF Specific Metadata/Tracking
    public IIncludableQueryable<TReturn, TProperty> Include<TReturn, TProperty>(Expression<Func<TReturn, TProperty>> navigationPropertyPath)
        where TReturn : class
        => EntityFrameworkQueryableExtensions.Include<TReturn, TProperty>(this.Cast<TReturn>(), navigationPropertyPath);
    public IQueryable<TReturn> AsNoTracking<TReturn>()
        where TReturn : class
        => EntityFrameworkQueryableExtensions.AsNoTracking(this.Cast<TReturn>());
    public IQueryable<TSource> TagWith(string tag)
        => EntityFrameworkQueryableExtensions.TagWith(this, tag);
    #endregion

    #region Bulk Operations
    public Task<int> ExecuteDeleteAsync(CancellationToken token = default)
        => EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(this, token);
    #endregion
}
