namespace DataShared.ForeignEntity;


public interface IEntity : IDisposable
{
    //abstract internal static IEntity Create(IEntity target);
    //abstract internal static IEntity Wrap(IEntity target);
    Task<IEntity?> Update(IEntity? entity = null);
    Task<TEntity?> Update<TEntity>(TEntity? entity = null, IQueryManager? query = null) 
        where TEntity : class, IEntity<TEntity>, IEntity;
    Task<IEntity> Save(IQueryManager? query = null);
    int? CanonicalFingerprint { get; set; }
    public IQueryManager? QueryManager { get; set; }
    public Type? ContextType { get; set; }



}

public interface IEntity<T> : IEntity where T : class, IEntity<T>
{

    static abstract List<PropertyInfo> Predicate { get; }
    static abstract List<PropertyInfo> Display { get; }
    static abstract List<PropertyInfo> Database { get; }
    static abstract List<PropertyInfo> Interesting { get; }

    //Task<T> Save(IServiceProvider service);
    //Task<T> Save(IQueryManager? query = null);
}
