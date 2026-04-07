namespace DataShared.ForeignEntity;

public static partial class IEntityExtensions
{

    public static async Task<IEntity?> Update(this IEntity? entity, IQueryManager? query = null)
    {
        if (entity == null)
        {
            return default!;
        }
        var Query = query ?? entity.QueryManager
            ?? throw new InvalidOperationException("No service provider.");

        return await Query.Update(Query.EphemeralStorage, entity);
    }


    public static async Task<T> Update<T>(this T entity, IQueryManager? query = null) where T : Entity<T>, IEntity
    {
        var Query = query ?? entity.QueryManager
            ?? throw new InvalidOperationException("No service provider.");
        return await Query.Update<T>(Query.EphemeralStorage, entity);
    }

    /// <summary>
    /// Rehydrates the entity by discarding local changes and fetching 
    /// the latest data from the database.
    /// </summary>
    public static async Task<T> Update<T>(this T? entity) where T : Entity<T>, IEntity
    {
        if (entity == null)
        {
            return default!;
        }
        var Query = entity.QueryManager
            ?? throw new InvalidOperationException("No service provider.");

        return await Query.Update(Query.EphemeralStorage, entity);
    }

    /*
    public static async Task<IEntity> Update(this IEntity? entity)
    {
        if (entity == null)
        {
            return default!;
        }
        if (QueryManager.Service == null)
        {
            throw new InvalidOperationException("No service provider.");
        }

        return await QueryManager.Service.GetRequiredService<IQueryManager>().Update(false, entity);
    }
    */

}
