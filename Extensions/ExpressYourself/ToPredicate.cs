using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace Extensions.PrometheusTypes
{
    public static partial class ExpressionExtensions
    {


        public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this IEntity entity)
            where TEntity : Entities.Entity<TEntity>
        {
            List<PropertyInfo> properties = Entities.Entity<TEntity>.Database;
            var members = properties.ToDictionary<PropertyInfo, MemberInfo, object?>(m => m, m => m.GetValue(entity));
            return ToPredicate<TEntity>(members);
        }


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

        public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this Dictionary<MemberInfo, object?> members)
            where TEntity : Entities.Entity<TEntity>
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




    }
}
