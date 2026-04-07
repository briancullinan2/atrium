namespace DataShared.ForeignEntity;

public static partial class IEntityExtensions
{

    public static T Create<T>(this IQueryManager Query) where T : Entity<T>, IEntity
    {
        return Create<T>(Query, Query.EphemeralStorage);
    }

    public static T Create<T>(this IQueryManager Query, StorageType storage) where T : Entity<T>, IEntity
    {
        var context = Query.GetContext(storage)
            ?? throw new InvalidOperationException("Could not render context: " + storage);

        var type = typeof(T);

        // Look for any parameterless constructor (Public or Private)
        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes, // Ensures it's the parameterless one
            modifiers: null)
            ?? throw new InvalidOperationException(
                $"No parameterless constructor found for {type.Name}.");

        var entity = constructor.Invoke(null) as T
            ?? throw new InvalidOperationException("Could not render entity: " + typeof(T));

        entity.QueryManager = Query;
        entity.ContextType = context.GetType();

        // Optionally: Automatically add it to the ChangeTracker
        context.Set<T>().Add(entity);

        return entity;
    }



    public static T Create<T>(this ITranslationContext context) where T : Entity<T>, IEntity<T>, IEntity
    {
        // Use reflection to hit the protected/private constructor
        var entity = Activator.CreateInstance(typeof(T), nonPublic: true) as T
            ?? throw new InvalidOperationException($"Could not instantiate {typeof(T).Name}");

        // Inject the context (The "Stamp")
        entity.QueryManager = context.Query;
        entity.ContextType = context.GetType();

        // Track it immediately
        context.Set<T>().Add(entity);

        return entity;
    }

}
