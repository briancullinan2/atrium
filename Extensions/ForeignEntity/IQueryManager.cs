
namespace Extensions.ForeignEntity;



[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum StorageType : int
{
    Ephemeral = 0,
    Persistent = 1,
    Remote = 2,
    Test = 3
}


public interface IQueryManager
{
    // TODO: for overriding in web client to switch persistent to remote, code reduction
    IQueryProvider? FinalProvider { get; set; }

    StorageType EphemeralStorage { get; set; }
    StorageType PersistentStorage { get; set; }
    Type EphemeralType { get; set; }
    Type PersistentType { get; set; }


    Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10)
        where TSet : Entity<TSet>;
    Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10)
        where TSet : Entity<TSet>;

    Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier, int priority = 10)
        where TSet : Entity<TSet>
        where TFrom : ITranslationContext
        where TTo : ITranslationContext;

    Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
    Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;
    Task<IEntity> Save(IEntity entity, int priority = 10);
    Task<IEntity> Save(StorageType storage, IEntity entity, int priority = 10);
    Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
    Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;



    //Task<IEntity> Save(bool persistent, IEntity entity, int priority = 10);


    Task<List<object>> Query(object query, string type, int priority = 10);
    IAsyncQueryable<TEntity> Query<TEntity>(object query, int priority = 10) where TEntity : Entity<TEntity>;
    IAsyncQueryable<TEntity> Query<TEntity>(Expression<Func<TEntity, bool>>? query = null, int priority = 10) where TEntity : Entity<TEntity>;
    //TResult Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
    //TResult Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;


    Task<TResult> QueryNow<TEntity, TResult>(
        StorageType storage,
        Expression query,
        int priority = 10)
        where TEntity : class;


    Task<TEntity> Update<TEntity, TResult>(StorageType storage, Expression<Func<TEntity, TResult>> key, int priority = 10) where TEntity : Entity<TEntity>;

    Task<TEntity> Update<TEntity>(Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
    Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
    Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

    Task<IEntity?> Update(IEntity entity, int priority = 10);
    Task<IEntity?> Update(StorageType storage, IEntity entity, int priority = 10);

    Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
    Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
    Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


    //Task ProcessQueueAsync();





    Expression? ToExpression(string query);
    Expression? ToExpression(StorageType? storage, string query);

    Expression? ToExpression(string query, out IQueryable? set);
    Expression? ToExpression(StorageType? storage, string query, out IQueryable? set);

    Task<object?> ToQueryable(string query);

    Task<object?> ToQueryable(string query, StorageType? storage);

    ITranslationContext GetContext(StorageType type);

    TContext GetContext<TContext>(StorageType? type = null) where TContext : DbContext;

}
