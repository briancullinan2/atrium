
namespace DataShared.ForeignEntity;

public partial class Entity<T>
{

    //public async Task<T> Save(IServiceProvider? service)
    //{
    //    return (T)(await IEntityExtensions.Save(this as T, service ?? Utilities.QueryManager.Service));
    //}



    public async Task<IEntity> Save(IQueryManager? query = null)
    {
        return (IEntity)(await IEntityExtensions.Save(this as T, query ?? QueryManager));
    }

    /*

    async Task<IEntity> IEntity.Save()
    {
        return await IEntityExtensions.Save(this);
    }
    */

    /*
    async Task<IEntity> IEntity.Update(IEntity? entity)
    {
        return await IEntityExtensions.Update(entity);
    }
    */

    public async Task<IEntity?> Update(IEntity? entity = null)
    {
        return await IEntityExtensions.Update(entity);
    }


    public async Task<T> Update(IQueryManager query)
    {
        return await IEntityExtensions.Update<T>((T)this, query);
    }

    public async Task<TEntity?> Update<TEntity>(TEntity? entity = null, IQueryManager? query = null) 
        where TEntity : class, IEntity<TEntity>, IEntity
    {
        if (entity == null && this is TEntity that)
        {
            return await IEntityExtensions.Update<TEntity>(that, query);
        }
        else if (entity != null)
        {
            return await IEntityExtensions.Update<TEntity>(entity, query);
        }
        else
            return default;
    }

    //async Task<TEntity> IEntity.Update<TEntity>(TEntity? entity) where TEntity : class
    //{
    //    return await IEntityExtensions.Update(entity!);
    //}

}

public static partial class IEntityExtensions
{




    public static string? Table(this Type type)
    {
        // has to match RemoteManager.SaveNow putRecord
        if (!type.Extends(typeof(Entity<>))) return null;
        return type.GetCustomAttributes().OfType<TableAttribute>().FirstOrDefault()?.Name ?? type.Name;
    }



    //public static T Wrap<T>(this T target) where T : class, IEntity<T>
    //{
    //    return T.Wrap(target, target._service);
    //}


    /*
    public static List<T> ToList<T>(this IQueryable<T> query) where T : IEntity, new()
    {
        // 1. Convert the IQueryable to a raw SQL string
        // If using EF Core: string sql = query.ToQueryString();
        // If using a custom metadata provider, call its specific SQL generator
        string sql = query.ToString();

        var results = new List<T>();

        // 2. Execute via ADO.NET for high-performance medical-grade streaming
        using (var conn = DataFactory.CreateConnection())
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Use your reflection-based mapper to hydrate the entity
                        results.Add(DataMapper.Map<T>(reader));
                    }
                }
            }
        }
        return results;
    }
    */



}