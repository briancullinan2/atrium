

using Microsoft.EntityFrameworkCore;

namespace DataShared.ForeignEntity;

public static partial class IEntityExtensions
{


    public static async Task<TEntity?> EntryAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntity>(this DbContext context, TEntity entity)
        where TEntity : Entity<TEntity>
    {
        var lambda = Predicate(entity);
        return await context.Set<TEntity>().FirstOrDefaultAsync(lambda);
    }



    public static async Task<bool> ExistsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntity>(this DbContext context, TEntity entity)
        where TEntity : Entity<TEntity>
    {
        return (await EntryAsync(context, entity)) != null;
    }

}
