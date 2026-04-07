
namespace Extensions.PrometheusTypes;

public static partial class ExpressionExtensions
{


    private static bool IsParameterResource(MemberExpression me)
    {
        // Drills down to the root. If the root is 's' in 's => s.Name', it's an entity property.
        // If the root is a 'Constant' (the closure class), it's a value/variable.
        Expression? root = me.Expression;
        while (root is MemberExpression next) root = next.Expression;
        while (root is UnaryExpression unary) root = unary.Operand;

        return root is ParameterExpression;
    }

    public static bool IsTerminal(this MethodInfo method)
    {
        var terminalMethods = new[]
        { 

            // Quantifiers (Booleans)
            nameof(Queryable.Any),
            nameof(Queryable.All),
            nameof(Queryable.Contains),

            // Element Operations (Single items)
            nameof(Queryable.First),
            nameof(Queryable.Single),
            nameof(Queryable.Last),
            nameof(Queryable.FirstOrDefault),
            nameof(Queryable.SingleOrDefault),
            nameof(Queryable.LastOrDefault),

            // Aggregates (Math/Counts)
            nameof(Queryable.Count),
            nameof(Queryable.LongCount),
            nameof(Queryable.Min),
            nameof(Queryable.Max),
            nameof(Queryable.Sum),
            nameof(Queryable.Average),


            // Collections & Arrays
            nameof(Enumerable.ToList),
            nameof(Enumerable.ToArray),
            nameof(Enumerable.ToDictionary),
            nameof(Enumerable.ToHashSet),

            // Iteration & Logic
            nameof(List<>.ForEach),



            // Collections & Arrays
            nameof(EntityFrameworkQueryableExtensions.ToListAsync),
            nameof(EntityFrameworkQueryableExtensions.ToArrayAsync),
            nameof(EntityFrameworkQueryableExtensions.ToDictionaryAsync),
            nameof(EntityFrameworkQueryableExtensions.ToHashSetAsync),

            // Quantifiers (Booleans)
            nameof(EntityFrameworkQueryableExtensions.AnyAsync),
            nameof(EntityFrameworkQueryableExtensions.AllAsync),
            nameof(EntityFrameworkQueryableExtensions.ContainsAsync),

            // Element Operations (Single items)
            nameof(EntityFrameworkQueryableExtensions.FirstAsync),
            nameof(EntityFrameworkQueryableExtensions.SingleAsync),
            nameof(EntityFrameworkQueryableExtensions.LastAsync),
            nameof(EntityFrameworkQueryableExtensions.FirstOrDefaultAsync),
            nameof(EntityFrameworkQueryableExtensions.SingleOrDefaultAsync),
            nameof(EntityFrameworkQueryableExtensions.LastOrDefaultAsync),

            // Aggregates (Math/Counts)
            nameof(EntityFrameworkQueryableExtensions.CountAsync),
            nameof(EntityFrameworkQueryableExtensions.LongCountAsync),
            nameof(EntityFrameworkQueryableExtensions.MinAsync),
            nameof(EntityFrameworkQueryableExtensions.MaxAsync),
            nameof(EntityFrameworkQueryableExtensions.SumAsync),
            nameof(EntityFrameworkQueryableExtensions.AverageAsync),

            // Iteration & Logic
            nameof(EntityFrameworkQueryableExtensions.ForEachAsync),

            // 2026 Batch Operations
            nameof(EntityFrameworkQueryableExtensions.ExecuteDeleteAsync),
            nameof(EntityFrameworkQueryableExtensions.ExecuteUpdateAsync)
        };
        return terminalMethods.Contains(method.Name);
    }


    public static bool IsTerminal(this MethodCallExpression node)
    {
        return node.Method.IsTerminal();
    }


    public static bool IsOrdering(this MethodInfo method)
    {
        var ordering = new[] {
            nameof(Queryable.OrderBy),
            nameof(Queryable.ThenBy),
            nameof(Queryable.ThenByDescending),
            nameof(Queryable.OrderByDescending),
        };
        return ordering.Contains(method.Name);
    }


    internal static List<string> PredicateMethods =
        [.. new List<Type>([typeof(EntityFrameworkQueryableExtensions)
            , typeof(Queryable)])
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        .Where(ExpressionExtensions.IsBoolean)
        .OrderBy(Selectors.OrderDatabaseQueries)
        .Select(m => m.Name)
        ];


    public static bool IsPredicate(this MethodInfo method)
    {
        return PredicateMethods.Contains(method.Name);
    }

    public static bool IsPredicate(this MethodCallExpression method)
    {
        return method.Method.IsPredicate();
    }


    public static bool IsSingular(this MethodInfo method)
    {
        var singular = new[] {
            nameof(Queryable.First),
            nameof(Queryable.FirstOrDefault),
            nameof(Queryable.Single),
            nameof(Queryable.SingleOrDefault),
            nameof(Queryable.Last),
            nameof(Queryable.LastOrDefault),

            nameof(EntityFrameworkQueryableExtensions.FirstAsync),
            nameof(EntityFrameworkQueryableExtensions.SingleAsync),
            nameof(EntityFrameworkQueryableExtensions.LastAsync),
            nameof(EntityFrameworkQueryableExtensions.FirstOrDefaultAsync),
            nameof(EntityFrameworkQueryableExtensions.SingleOrDefaultAsync),
            nameof(EntityFrameworkQueryableExtensions.LastOrDefaultAsync),
        };
        return singular.Contains(method.Name);
    }



    public static bool IsProjection(this MethodCallExpression method)
    {
        return method.Method.IsProjection();
    }

    public static bool IsProjection(this MethodInfo method)
    {
        var projection = new[] {
            nameof(Queryable.Select),
            nameof(Queryable.SelectMany),
            nameof(Queryable.GroupBy),
            nameof(Queryable.Join),
            nameof(Queryable.GroupJoin)


        };
        return projection.Contains(method.Name);
    }


    public static bool IsFilter(this MethodInfo method)
    {
        var filtering = new[] {
            nameof(Queryable.Where),
        };
        return filtering.Contains(method.Name);
    }



    public static bool IsIdentity(this Expression expression)
    {
        // 1. Unwrap the Lambda if it's the root
        if (expression is LambdaExpression lambda)
        {
            var body = lambda.Body;

            // 2. Peel off any 'Convert' or 'Quote' wrappers
            while (body.NodeType == ExpressionType.Convert ||
                   body.NodeType == ExpressionType.ConvertChecked ||
                   body.NodeType == ExpressionType.Quote)
            {
                body = ((UnaryExpression)body).Operand;
            }

            // 3. If the body is exactly the first parameter, it's a full set request
            // Example: entities => (AsyncQueryable<User>)entities
            if (body == lambda.Parameters[0])
            {
                return true;
            }
        }

        return false;
    }



    public static bool IsSingular(this Expression expression)
    {
        if (expression is MethodCallExpression m)
        {
            return m.Method.IsSingular();
        }
        return false;
    }



    public static bool IsScalar(this Expression expression)
    {
        if (expression is MethodCallExpression m)
        {
            return m.Method.IsScalar();
        }
        return false;
    }

    /// <summary>
    /// Returns true if the expression produces a single result 
    /// (Scalar, Element operator, or Boolean check).
    /// </summary>
    public static bool IsTerminal(this Expression expression)
    {
        return expression.IsSingular() || expression.IsScalar();
    }




    public static bool IsScalar(this MethodInfo method)
    {
        var scalars = new[] {
            nameof(Queryable.Any),
            nameof(Queryable.All),
            nameof(Queryable.Contains),

            nameof(Queryable.Count),
            nameof(Queryable.LongCount),
            nameof(Queryable.Min),
            nameof(Queryable.Max),
            nameof(Queryable.Sum),
            nameof(Queryable.Average),

            nameof(EntityFrameworkQueryableExtensions.AnyAsync),
            nameof(EntityFrameworkQueryableExtensions.AllAsync),
            nameof(EntityFrameworkQueryableExtensions.ContainsAsync),

            nameof(EntityFrameworkQueryableExtensions.CountAsync),
            nameof(EntityFrameworkQueryableExtensions.LongCountAsync),
            nameof(EntityFrameworkQueryableExtensions.MinAsync),
            nameof(EntityFrameworkQueryableExtensions.MaxAsync),
            nameof(EntityFrameworkQueryableExtensions.SumAsync),
            nameof(EntityFrameworkQueryableExtensions.AverageAsync),

        };
        return scalars.Contains(method.Name);
    }



    public static bool IsDefault(this MethodInfo method)
    {
        var defaultMethods = new[]
        {
            nameof(Queryable.FirstOrDefault),
            nameof(Queryable.SingleOrDefault),
            nameof(Queryable.LastOrDefault),

            nameof(EntityFrameworkQueryableExtensions.FirstOrDefaultAsync),
            nameof(EntityFrameworkQueryableExtensions.SingleOrDefaultAsync),
            nameof(EntityFrameworkQueryableExtensions.LastOrDefaultAsync),
        };

        return defaultMethods.Contains(method.Name);
    }


    public static bool IsDefault(this Expression expression)
    {
        if (expression is MethodCallExpression m)
        {
            return m.Method.IsDefault();
        }
        return false;
    }


    public static bool IsArithmetic(this BinaryExpression node) => node.NodeType switch
    {
        ExpressionType.Add or ExpressionType.Subtract or
        ExpressionType.Multiply or ExpressionType.Divide or
        ExpressionType.Modulo or ExpressionType.Power => true,
        _ => false
    };

    public static bool IsBitwise(this BinaryExpression node) => node.NodeType switch
    {
        ExpressionType.And or ExpressionType.Or or
        ExpressionType.ExclusiveOr => true,
        _ => false
    };

    public static bool IsCompare(this BinaryExpression node) => node.NodeType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual or
        ExpressionType.LessThan or ExpressionType.LessThanOrEqual or
        ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual => true,
        _ => false
    };



    public static bool IsBoolean(this MethodInfo method)
    {
        return method.GetParameters()
            .FirstOrDefault(p => p.ParameterType.Extends(typeof(Expression<>)))
            is ParameterInfo parameter
            && parameter.ParameterType.GetGenericArguments().FirstOrDefault()
            is Type funcType
            && funcType.Extends(typeof(Func<>))
            && funcType.GetGenericArguments().LastOrDefault() == typeof(bool);
    }




    public static bool IsBoolean(this BinaryExpression node) => node.NodeType switch
    {
        ExpressionType.AndAlso or ExpressionType.OrElse or
        ExpressionType.Not => true,
        _ => false
    };
}
