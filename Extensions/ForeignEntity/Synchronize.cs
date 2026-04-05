
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Extensions.ForeignEntity
{
    public interface ITranslationContext
    {

        IQueryManager Query { get; set; }
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        Task InitializeIfNeeded();
        DatabaseFacade Database { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
        IModel Model { get; }
        ChangeTracker ChangeTracker { get; }
    }


    public static partial class IEntityExtensions
    {
        /*
        public static async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(Expression<Func<TSet, bool>> qualifier)
            where TFrom : TranslationContext
            where TTo : TranslationContext
            where TSet : Entity<TSet>
        {
            if (contextFrom.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            var Query = contextFrom.Service.GetRequiredService<IQueryManager>();
            return await Query.Synchronize(contextFrom, contextTo, qualifier);
        }
        */

        public static async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(this TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier)
            where TFrom : ITranslationContext
            where TTo : ITranslationContext
            where TSet : Entity<TSet>
        {
            if (contextFrom.Query == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            // TODO: contextTO?
            return await contextFrom.Query.Synchronize(contextFrom, contextTo, qualifier);
        }


    }
}
