using DataLayer.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;


namespace DataLayer.Utilities.Extensions
{
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


        public static ConstantExpression ToConstant(this Type left, string Term)
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

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && (p.PropertyType == typeof(string) || p.PropertyType.IsPrimitive || p.PropertyType.IsEnum));

            foreach (var prop in props)
            {
                yield return string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

                // If it's a complex type (but not string/primitive), recurse
                if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                {
                    // Optional: check for a [Searchable] attribute here
                    foreach (var child in ToSearch(prop.PropertyType, maxDepth - 1, prop.Name))
                        yield return child;
                }
            }
        }

    }
}
