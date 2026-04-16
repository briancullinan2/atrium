
namespace Extensions.PrometheusTypes;

public static partial class ExpressionExtensions
{



    public static Expression<Func<object, object?>>? ToAccessor(this string columnName, Type entityType)
    {
        // Call the generic version via reflection
        var genericMethod = typeof(ExpressionExtensions)
            .GetMethod(nameof(ToAccessor), [typeof(string)])?
            .MakeGenericMethod(entityType);

        var genericExpr = genericMethod?.Invoke(null, [columnName]);

        if (genericExpr == null) return null;

        // We must transform Expression<Func<TEntity, object>> 
        // into Expression<Func<object, object>>
        var param = Expression.Parameter(typeof(object), "ent");
        var castParam = Expression.Convert(param, entityType);

        // Invoke the generic expression using the casted parameter
        var invocation = Expression.Invoke((Expression)genericExpr, castParam);

        return Expression.Lambda<Func<object, object?>>(invocation, param);
    }

    public static Expression<Func<TEntity, object?>> ToAccessor<TEntity>(this string columnName)
    {
        var type = typeof(TEntity);
        var entityParam = Expression.Parameter(type, "ent");

        // 1. Get the MemberInfo (Property or Field)
        MemberInfo? member = type.GetProperty(columnName) as MemberInfo ?? type.GetField(columnName);

        if (member == null)
        {
            return ent => null;
        }

        // 2. Create the property/field access: ent.ColumnName
        var access = Expression.MakeMemberAccess(entityParam, member);

        // 3. Box to object (since properties/fields might be ValueTypes)
        var boxedAccess = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<TEntity, object?>>(boxedAccess, entityParam);
    }




}
