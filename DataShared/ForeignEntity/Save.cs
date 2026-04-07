
namespace DataShared.ForeignEntity;

public static partial class IEntityExtensions
{

    /*
    public static async Task<IEntity> Save(this IEntity? ent)
    {
        if (ent == null)
        {
            return default!;
        }
        if (QueryManager.Service == null)
        {
            throw new InvalidOperationException("No service provider.");
        }

        return await QueryManager.Service.GetRequiredService<IQueryManager>().Save(false, ent);
    }
    */

    /*
    public static async Task<T> Save<T>(this T? ent) where T : Entity<T>, IEntity<T>, IEntity
    {
        if (ent == null)
        {
            return default!;
        }
        var Query = ent.QueryManager
            ?? QueryManager.Service?.GetService(typeof(IQueryManager)) as IQueryManager
            ?? throw new InvalidOperationException("No service provider.");
        return await Query.Save(Query.EphemeralStorage, ent);
    }
    */

    public static async Task<T> Save<T>(this T? ent, IServiceProvider? Service = null) where T : Entity<T>, IEntity<T>, IEntity
    {
        if (ent == null)
        {
            return default!;
        }
        var Query = Service?.GetRequiredService<IQueryManager>() ?? ent.QueryManager
            ?? throw new InvalidOperationException("No query manager.");
        return await Query.Save(Query.EphemeralStorage, ent);
    }

    public static async Task<T> Save<T>(this T? ent, IQueryManager? query = null) where T : Entity<T>, IEntity<T>, IEntity
    {
        if (ent == null)
        {
            return default!;
        }
        var Query = query ?? ent.QueryManager ?? throw new InvalidOperationException("No query manager.");
        return await Query.Save(Query.EphemeralStorage, ent);
    }

}
