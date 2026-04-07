

using System.Diagnostics.CodeAnalysis;

namespace Extensions.QueryableChaos;

public static partial class QueryableExtensions
{
    public enum CombineMode
    {
        OrElse,  // Default: Any match
        AndAlso  // Strict: All must match
    }

    /// <summary>
    /// Folds multiple expressions into a single tree based on the specified mode.
    /// </summary>
    /// <param name="expressions">The list of property/value expressions to combine.</param>
    /// <param name="mode">Whether to use OR or AND logic between elements.</param>
    /// <param name="isNegated">If true, the entire resulting group is wrapped in Expression.Not.</param>
    public static Expression? Combine(
        CombineMode mode = CombineMode.OrElse,
        bool isNegated = false,
        params IEnumerable<Expression> expressions
    )
    {
        if (expressions == null || !expressions.Any())
            return null;

        Expression? root = null;

        foreach (var expr in expressions)
        {
            if (expr == null) continue;

            if (root == null)
            {
                root = expr;
            }
            else
            {
                root = mode == CombineMode.AndAlso
                    ? Expression.AndAlso(root, expr)
                    : Expression.OrElse(root, expr);
            }
        }

        return isNegated ? Expression.Not(root!) : root;
    }


    [GeneratedRegex(@"[~!](?<exclusion>[^\s]+)|(?<column>(?<columnName>[^\s]+):([~!](?<columnExclusion>[^\s]+)|(?<columnTerm>[^\s]+)))|(?<term>[^\s]+)", RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex KeyValueSearchParser();


    public static string? Success(this GroupCollection collection, string name)
        => collection[name].Success ? collection[name].Value : null;


    public static (string Name, string Term, bool Exclusion) ParseSearch(string? search)
        => ParseSearch(KeyValueSearchParser().Match(search ?? string.Empty));


    public static (string Name, string Term, bool Exclusion) ParseSearch(Match? match)
        => match != null && match.Success ? (
            Name: match.Groups.Success("columnName") ?? string.Empty,
            Term: match.Groups.Success("exclusion") ?? match.Groups.Success("columnExclusion")
                ?? match.Groups.Success("columnTerm") ?? match.Groups.Success("term") ?? string.Empty,
            match.Groups.Success("exclusion") != null || match.Groups.Success("columnExclusion") != null)
        : (
            Name: string.Empty,
            Term: string.Empty,
            false
        );


    public static TResult Map<TSource, TResult>(this TSource source, Func<TSource, TResult> func) => func(source);


    public static ConstantExpression ToConstant(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        this Type left, string Term)
        => (Nullable.GetUnderlyingType(left) ?? left) switch
        {
            var t when typeof(Enum).IsAssignableFrom(t) => Expression.Constant(Term.TryParse(t.GetType())),
            var t when typeof(string).IsAssignableFrom(t) => Expression.Constant(Term),
            var t when typeof(bool).IsAssignableFrom(t) => Expression.Constant(Term.TryParse()),
            var t when TypeDescriptor.GetConverter(t) is TypeConverter converter
                && converter.CanConvertFrom(typeof(string))
                => Expression.Constant(converter.ConvertFromInvariantString(Term), left),


            _ => throw new InvalidOperationException("Dont know that type.")
        };


    public static ConstantExpression? ToConstant(this string Term, Type left) => left.ToConstant(Term);

    public static Expression ToEquality(this Expression left, string? search)
        => ParseSearch(search) switch
        {
            // TODO: add ToMember from left to Name
            (var Name, var Term, false) when string.IsNullOrWhiteSpace(Name) => Expression.Equal(left, left.Type.ToConstant(Term)),
            (var Name, var Term, true) when string.IsNullOrWhiteSpace(Name) => Expression.NotEqual(left, left.Type.ToConstant(Term)),
            (var Name, var Term, false) => left.ToMember(Name, Term),
            (var Name, var Term, true) => Expression.Not(left.ToMember(Name, Term))
        };

    public static Expression ToEquality(this string? search, Expression left) => left.ToEquality(search);




    public static IQueryable<T> Search<T>(this IQueryable<T> query, string? search, CombineMode? mode = null)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;

        var parameter = Expression.Parameter(typeof(T), "e");
        var matches = KeyValueSearchParser().Matches(search);

        var expressions = matches.Select(m =>
        {
            var (name, term, isExcl) = ParseSearch(m);
            if (string.IsNullOrEmpty(term)) return null;

            // If no column name, you'd iterate through 'searchable' properties here.
            // For now, let's assume 'name' is provided.
            Expression? resultExpr = null;
            if (string.IsNullOrEmpty(name)
                && typeof(IEntity).IsAssignableFrom(typeof(T)))
            {
                // TODO: search all visible columns
                var recommended = typeof(Entity<>).GetProperty(nameof(Entity<>.Database))?.GetValue(null, null) as List<PropertyInfo>;
                var expressions = recommended?.Select(propertyInfo => parameter.ToMember(propertyInfo.Name, term));
                if (expressions == null || !expressions.Any()) return null;
                resultExpr = Combine(mode ?? CombineMode.OrElse, false, expressions);
            }
            else
            {
                resultExpr = parameter.ToMember(name, term);
            }

            if (resultExpr == null) return null;


            // If resultExpr is already a MethodCall (like Any), don't wrap it in Equality again
            if (resultExpr.Type != typeof(bool))
            {
                resultExpr = resultExpr.ToEquality(term);
            }

            return isExcl ? Expression.Not(resultExpr) : resultExpr;
        }).Where(e => e != null);

        var body = Combine(mode ?? CombineMode.OrElse, false, expressions!);
        return body == null ? query : query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }



    private static IOrderedQueryable<TEntity> SortBy<TEntity>(
        IQueryable<TEntity> source,
        Expression body,
        ParameterExpression parameter,
        bool isAscending,
        bool isFirst)
    {
        string methodName = isFirst
            ? (isAscending ? nameof(Queryable.OrderBy) : nameof(Queryable.OrderByDescending))
            : (isAscending ? nameof(Queryable.ThenBy) : nameof(Queryable.ThenByDescending));

        var lambda = Expression.Lambda(body, parameter);

        // This is where we 'cheat' the TKey. We pass body.Type as the second generic argument.
        var methodCall = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(TEntity), body.Type],
            isFirst ? source.Expression : ((IOrderedQueryable<TEntity>)source).Expression,
            Expression.Quote(lambda)
        );

        return (IOrderedQueryable<TEntity>)source.Provider.CreateQuery<TEntity>(methodCall);
    }


    public static IOrderedQueryable<TEntity> SortBy<TEntity>(
        this IQueryable<TEntity> source,
        params (string Path, bool IsAscending)[] sorts)
    {
        IOrderedQueryable<TEntity> current = null!;
        var parameter = Expression.Parameter(typeof(TEntity), "e");

        for (int i = 0; i < sorts.Length; i++)
        {
            // Reuse your high-end ToMember logic here
            var body = parameter.ToMember(sorts[i].Path);
            current = SortBy(source, body, parameter, sorts[i].IsAscending, i == 0);
            source = current; // Update source for the next 'ThenBy'
        }

        return current ?? (IOrderedQueryable<TEntity>)source;
    }

    public static IOrderedQueryable<TEntity> SortBy<TEntity>(
        this IQueryable<TEntity> source,
        params (Expression<Func<TEntity, object>> KeySelector, bool IsAscending)[] sorts)
    {
        IOrderedQueryable<TEntity> current = null!;

        for (int i = 0; i < sorts.Length; i++)
        {
            var lambda = sorts[i].KeySelector;
            // Unbox 'object' to get the real underlying type (e.g. DateTime)
            var body = lambda.Body is UnaryExpression u ? u.Operand : lambda.Body;
            var parameter = lambda.Parameters[0];

            current = SortBy(source, body, parameter, sorts[i].IsAscending, i == 0);
            source = current;
        }

        return current ?? (IOrderedQueryable<TEntity>)source;
    }

    public static IEnumerable<string> ToSearch<TEntity>(this TEntity _, int maxDepth = 2, string prefix = "")
        where TEntity : Entity<TEntity>
    {
        return ToSearch(typeof(TEntity), maxDepth, prefix);
    }

    public static IEnumerable<string> ToSearch(this Type type, int maxDepth = 2, string prefix = "")
    {
        if (maxDepth == 0) yield break;

        var props = type.GetProperties(null)
            .Where(p => p.CanRead && p.PropertyType.IsSimple());

        foreach (var prop in props)
        {
            yield return string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            // If it's a complex type (but not string/primitive), recurse
            if (!prop.PropertyType.IsSimple())
            {
                // Optional: check for a [Searchable] attribute here
                foreach (var child in ToSearch(prop.PropertyType, maxDepth - 1, prop.Name))
                    yield return child;
            }
        }
    }



    public static TResult ToForced<TEntity, TResult>(this IQueryable<TEntity> entity)
    {
        // 1. Setup the Query and Provider
        var set = entity; // Assuming entity is the root set
        var selector = set.Expression as Expression<Func<TEntity, TResult>>
            ?? throw new InvalidOperationException("Can't cast expression: " 
                + set.Expression.ToString() + " to " + typeof(TResult).FullName);
        var query = selector.Body;
        var provider = set.Provider;

        // 2. Identify the target type (unwrapping Task/ValueTask if necessary)
        var resultType = typeof(TResult);
        var isAsync = typeof(Task).IsAssignableFrom(resultType) || resultType.Name.StartsWith("ValueTask");
        var underlyingType = isAsync ? resultType.GetGenericArguments().FirstOrDefault() ?? typeof(object) : resultType;

        // 3. Execution Logic
        object? executionResult = null;

        // Case A: It's a sequence (IQueryable) that needs ToListAsync
        if (typeof(IEnumerable).IsAssignableFrom(underlyingType) && underlyingType != typeof(string))
        {
            var finalQueryable = provider.CreateQuery(query);
            var elementType = finalQueryable.ElementType;

            // Use EF Core's ToListAsync extension via reflection
            var toListMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods(nameof(EntityFrameworkQueryableExtensions.ToListAsync), 1)
                .First()
                .MakeGenericMethod(elementType);

            executionResult = toListMethod.Invoke(null, [finalQueryable, null]); // Returns Task<List<T>>
        }
        // Case B: It's a scalar (Any, Count, First) using the Async Provider
        else if (provider is IAsyncQueryProvider asyncProvider)
        {
            var executeMethod = typeof(IAsyncQueryProvider)
                .GetMethods()
                .First(m => m.Name == nameof(IAsyncQueryProvider.ExecuteAsync) && m.IsGenericMethod)
                .MakeGenericMethod(isAsync ? resultType : typeof(Task<>).MakeGenericType(underlyingType));

            executionResult = executeMethod.Invoke(asyncProvider, [query, null]);
        }
        // Case C: Sync Fallback
        else
        {
            executionResult = provider.Execute(query);
        }

        // 4. Final Conversion & Task Wrapping
        if (isAsync)
        {
            // If we already have a Task from the provider, return it
            if (executionResult is TResult asyncResult) return asyncResult;

            // Otherwise, wrap the sync result in a Task.FromResult
            var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult))?.MakeGenericMethod(underlyingType);
            return (TResult)fromResultMethod?.Invoke(null, [executionResult])!;
        }

        // If it's a sync call but we got a Task back (e.g. from ToListAsync), we must wait
        if (executionResult is Task task)
        {
            task.GetAwaiter().GetResult(); // Block safely only in sync context
            var taskResult = ((dynamic)task).Result;
            return (TResult)CollectionConverter.ConvertAsync(taskResult, typeof(TResult))!;
        }

        return (TResult)executionResult!;
    }


}
