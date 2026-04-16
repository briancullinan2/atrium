


namespace Extensions.PrometheusTypes;

public static partial class ExpressionExtensions
{


    public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this Expression<TEntity> ex)
        where TEntity : class
    {
        return ToPredicate<TEntity>(ex.ToMembers());
    }


    public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this object? match)
        where TEntity : class
    {
        var type = typeof(TEntity);
        var parameter = Expression.Parameter(type, "e");

        if (match == null)
        {
            if (!typeof(TEntity).IsNullable())
            {
                return Expression.Lambda<Func<TEntity, bool>>(Expression.Constant(true), parameter);
            }
            var equality = Expression.NotEqual(parameter, Expression.Constant(null, typeof(TEntity)));
            return Expression.Lambda<Func<TEntity, bool>>(equality, parameter);
        }
        var members = match.ToMembers().ToDictionary(m => m.Key.Name, m => m.Value);
        var properties = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p =>
                !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                && !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute))
                )
            .OrderBy(p => p.Name)
            .ToList();


        Expression? predicate = null;

        foreach (var prop in properties)
        {
            // Check if our MemberInit dictionary actually contains this key
            if (!members.TryGetValue(prop.Name, out var value))
            {
                continue;
            }

            // Build: e.Prop == Value
            var left = Expression.Property(parameter, prop);
            var right = Expression.Constant(value, prop.PropertyType);
            var comparison = Expression.Equal(left, right);

            predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
        }

        return Expression.Lambda<Func<TEntity, bool>>(predicate!, parameter);
    }



}
