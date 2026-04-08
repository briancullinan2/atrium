using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DataShared.ForeignEntity;

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
    //List<Type> EntityTypes { get; }
}

public interface IHasEntityTypes
{
    static abstract List<Type> EntityTypes { get; }
}

public interface IHasValue
{
    string? Value { get; set; }
}

public interface IHasLogo
{
    int? LogoId { get; set; }
}

public interface IHasUser
{
    string? UserId { get; set; }
}

public interface IHasGroup
{
    int? GroupId { get; set; }
}

public interface IHasService
{
    static abstract IServiceProvider Services { get; }
}

public interface IHasService<T> : IHasService
{
    static abstract T? Current { get; }
}
