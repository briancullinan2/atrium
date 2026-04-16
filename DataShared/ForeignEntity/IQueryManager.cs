using Microsoft.EntityFrameworkCore;

namespace DataShared.ForeignEntity;



public interface IQueryManager
{
    // TODO: for overriding in web client to switch persistent to remote, code reduction
    IQueryProvider? FinalProvider { get; set; }

    Type EphemeralStorage { get; set; }
    Type PersistentStorage { get; set; }
    Type RemoteStorage { get; set; }
    Type TestStorage { get; set; }


    Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10)
        where TSet : class, IEntity<TSet>;
    Task<List<TSet>> Synchronize<TSet>(Type From, Type To, Expression<Func<TSet, bool>> qualifier, int priority = 10)
        where TSet : class, IEntity<TSet>;

    Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier, int priority = 10)
        where TSet : class, IEntity<TSet>
        where TFrom : ITranslationContext
        where TTo : ITranslationContext;

    Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<TEntity> Save<TEntity>(TEntity IEntity, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<IEntity> Save(IEntity IEntity, int priority = 10);
    Task<IEntity> Save(Type storage, IEntity IEntity, int priority = 10);
    Task<TEntity> Save<TEntity>(Type storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<TEntity> Save<TEntity>(Type storage, TEntity IEntity, int priority = 10) where TEntity : class, IEntity<TEntity>;



    //Task<IEntity> Save(bool persistent, IEntity IEntity, int priority = 10);


    Task<List<object>> Query(object query, string type, int priority = 10);
    IAsyncQueryable<TEntity> Query<TEntity>(object query, int priority = 10) where TEntity : class, IEntity<TEntity>;
    IAsyncQueryable<TEntity> Query<TEntity>(Expression<Func<TEntity, bool>>? query = null, int priority = 10) where TEntity : class, IEntity<TEntity>;
    //TResult Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : IEntity<TEntity>;
    //TResult Query<TEntity, TResult>(Type storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : IEntity<TEntity>;


    Task<TResult> QueryNow<TEntity, TResult>(
        Type storage,
        Expression query,
        int priority = 10)
        where TEntity : class;


    Task<TEntity> Update<TEntity, TResult>(Type storage, Expression<Func<TEntity, TResult>> key, int priority = 10) where TEntity : class, IEntity<TEntity>;

    Task<TEntity> Update<TEntity>(Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<TEntity> Update<TEntity>(TEntity IEntity, int priority = 10) where TEntity : class, IEntity<TEntity>;

    Task<IEntity?> Update(IEntity IEntity, int priority = 10);
    Task<IEntity?> Update(Type storage, IEntity IEntity, int priority = 10);

    Task<TEntity> Update<TEntity>(Type storage, Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<TEntity> Update<TEntity>(Type storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : class, IEntity<TEntity>;
    Task<TEntity> Update<TEntity>(Type storage, TEntity IEntity, int priority = 10) where TEntity : class, IEntity<TEntity>;


    //Task ProcessQueueAsync();





    Expression? ToExpression(string query);
    Expression? ToExpression(Type? storage, string query);

    Expression? ToExpression(string query, out IQueryable? set);
    Expression? ToExpression(Type? storage, string query, out IQueryable? set);

    Task<object?> ToQueryable(string query);

    Task<object?> ToQueryable(string query, Type? storage);

    ITranslationContext? GetContext(Type type);

    TContext GetContext<TContext>(Type? type = null) where TContext : DbContext;

}
