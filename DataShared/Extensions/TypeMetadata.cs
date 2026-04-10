namespace DataShared.Extensions;

public static partial class TypeExtensions
{

    private static readonly Dictionary<Type, EntityMetadata> _metadataCache = [];


    // there's another ToPredicate in Extensions.TypeExtensions that turns an object or Dictionary into a Boolean comparator
    public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this IEntity entity)
        where TEntity : Entity<TEntity>
    {
        List<PropertyInfo> properties = Entity<TEntity>.Database;
        var members = properties.ToDictionary<PropertyInfo, MemberInfo, object?>(m => m, m => m.GetValue(entity));
        return ToPredicate<TEntity>(members);
    }


    public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this Dictionary<MemberInfo, object?> members)
        where TEntity : Entity<TEntity>
    {
        var type = typeof(TEntity);
        var parameter = Expression.Parameter(type, "e");

        // 1. Identify the Keys (Mirroring your logic)
        var keyProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<KeyAttribute>() != null ||
                        p.Name == "Id" ||
                        p.Name == $"{type.Name}Id")
            .ToList();

        if (keyProperties.Count == 0)
            throw new InvalidOperationException($"Entity {type.Name} has no identifiable Primary Key.");

        Expression? predicate = null;

        foreach (var prop in keyProperties)
        {
            // Check if our MemberInit dictionary actually contains this key
            if (!members.TryGetValue(prop, out var value))
            {
                // In Arizona, an incomplete contract is unenforceable.
                // In EF, an incomplete key is unqueryable.
                throw new ArgumentException($"MemberInit is missing required Primary Key: {prop.Name}");
            }

            // Build: e.Prop == Value
            var left = Expression.Property(parameter, prop);
            var right = Expression.Constant(value, prop.PropertyType);
            var comparison = Expression.Equal(left, right);

            predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
        }

        return Expression.Lambda<Func<TEntity, bool>>(predicate!, parameter);
    }



    [Obsolete("This probably isn't what you want, Metadata of a Metadata?")]
    public static EntityMetadata Metadata(this EntityMetadata any)
    {
        return any;
    }


    public static EntityMetadata Metadata(this object any)
    {
        return any.GetType().Metadata();
    }

    public static EntityMetadata Metadata(this Type any)
    {
        if (_metadataCache.TryGetValue(any, out var meta))
            return meta;
        var newMeta = new EntityMetadata(any);
        _metadataCache.TryAdd(any, newMeta);
        return newMeta;
    }



}


internal static class CopiedTypeExtensions
{


    public static bool IsConcrete(this Type type)
    {
        if (type == null) return false;

        return !type.IsAbstract &&
               !type.IsInterface &&
               !type.IsGenericTypeDefinition;
    }


}
