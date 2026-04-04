namespace Extensions.ForeignEntity
{

    public interface IAsyncQueryable<TSource>
    {
        /// <summary>
        /// An interface representation of EntityFrameworkQueryableExtensions 
        /// for swappable data provider logic.
        /// </summary>
        string ToQueryString(IQueryable source);

        #region Any/All
        Task<bool> AnyAsync(CancellationToken cancellationToken = default);
        Task<bool> AnyAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        Task<bool> AllAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        #endregion

        #region Count/LongCount
        Task<int> CountAsync(CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        Task<long> LongCountAsync(CancellationToken cancellationToken = default);
        Task<long> LongCountAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        #endregion

        #region ElementAt
        Task<TSource> ElementAtAsync(int index, CancellationToken cancellationToken = default);
        Task<TSource?> ElementAtOrDefaultAsync(int index, CancellationToken cancellationToken = default);
        #endregion

        #region First/FirstOrDefault
        Task<TSource> FirstAsync(CancellationToken cancellationToken = default);
        Task<TSource> FirstAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        Task<TSource?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
        Task<TSource?> FirstOrDefaultAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        #endregion

        #region Last/LastOrDefault
        Task<TSource> LastAsync(CancellationToken cancellationToken = default);
        Task<TSource> LastAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        Task<TSource?> LastOrDefaultAsync(CancellationToken cancellationToken = default);
        Task<TSource?> LastOrDefaultAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        #endregion

        #region Single/SingleOrDefault
        Task<TSource> SingleAsync(CancellationToken cancellationToken = default);
        Task<TSource> SingleAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        Task<TSource?> SingleOrDefaultAsync(CancellationToken cancellationToken = default);
        Task<TSource?> SingleOrDefaultAsync(Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default);
        #endregion

        #region Min/Max/Sum/Average
        Task<TSource> MinAsync(CancellationToken cancellationToken = default);
        Task<TResult> MinAsync<TResult>(Expression<Func<TSource, TResult>> selector, CancellationToken cancellationToken = default);
        Task<TSource> MaxAsync(CancellationToken cancellationToken = default);
        Task<TResult> MaxAsync<TResult>(Expression<Func<TSource, TResult>> selector, CancellationToken cancellationToken = default);

        // Example for Sum (Repeat for other numeric types as needed)
        Task<decimal> SumAsync(CancellationToken cancellationToken = default);
        Task<decimal> SumAsync(Expression<Func<TSource, decimal>> selector, CancellationToken cancellationToken = default);

        Task<decimal> AverageAsync(CancellationToken cancellationToken = default);
        #endregion

        #region Collections
        Task<List<TSource>> ToListAsync(CancellationToken cancellationToken = default);
        Task<TSource[]> ToArrayAsync(CancellationToken cancellationToken = default);
        Task<HashSet<TSource>> ToHashSetAsync(CancellationToken cancellationToken = default);
        #endregion

        #region EF Specific Metadata/Tracking
        IIncludableQueryable<TReturn, TProperty> Include<TReturn, TProperty>(Expression<Func<TReturn, TProperty>> navigationPropertyPath) where TReturn : class;
        IQueryable<TReturn> AsNoTracking<TReturn>() where TReturn : class;
        IQueryable<TSource> TagWith(string tag);
        #endregion

        #region Bulk Operations
        Task<int> ExecuteDeleteAsync(CancellationToken cancellationToken = default);
        #endregion
    }

}
