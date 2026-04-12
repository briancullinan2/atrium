using Interfacing.Services;

namespace DataShared.ForeignEntity;

public interface ITranslationContext : IHasModule
{

    IQueryManager Query { get; set; }
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
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

