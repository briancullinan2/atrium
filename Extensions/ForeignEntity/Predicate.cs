using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Extensions.ForeignEntity
{
    public static partial class IEntityExtensions
    {



        public static List<PropertyInfo> Predicate(this Type type)
        {
            var primaryKey = type.GetCustomAttribute<PrimaryKeyAttribute>()?.PropertyNames;
            var keyProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<KeyAttribute>() != null || primaryKey?.Contains(p.Name) == true)
                .ToList();
            return keyProperties;
        }



        public static Expression<Func<TEntity, bool>> PredicateByFingerprint<TEntity>(this TEntity entity)
            where TEntity : Entity<TEntity>
        {
            var type = typeof(TEntity);

            // 1. Locate the Fingerprint property via Reflection
            // We assume the property name is "CanonicalFingerprint" based on your Entity definition
            var fingerprintProp = type.GetProperty("CanonicalFingerprint", BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"Entity type {type.Name} does not define a CanonicalFingerprint.");

            // 2. Build the Expression: e => e.CanonicalFingerprint == entity.CanonicalFingerprint
            var parameter = Expression.Parameter(type, "e");

            // Left side: e.CanonicalFingerprint
            var left = Expression.Property(parameter, fingerprintProp);

            // Right side: the constant value from our current entity instance
            var fingerprintValue = fingerprintProp.GetValue(entity);
            var right = Expression.Constant(fingerprintValue, fingerprintProp.PropertyType);

            // The comparison: e.CanonicalFingerprint == "..."
            var comparison = Expression.Equal(left, right);

            var lambda = Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);

            // 3. Execute against the ChangeTracker or the Database
            return lambda;
        }


        public static Expression<Func<TEntity, bool>> Predicate<TEntity>(this TEntity entity)
            where TEntity : Entity<TEntity>
        {
            var type = typeof(TEntity);

            // 1. Find properties via Reflection that have the [Key] attribute
            var keyProperties = Entity<TEntity>.Predicate;

            // Fallback: If no [Key] attribute, check for "Id" or "{ClassName}Id" 
            // to match EF's default convention
            if (keyProperties.Count == 0)
            {
                var idProp = type.GetProperty("Id") ?? type.GetProperty($"{type.Name}Id");
                if (idProp != null) keyProperties.Add(idProp);
            }

            if (keyProperties.Count == 0) return PredicateByFingerprint(entity);

            var parameter = Expression.Parameter(type, "e");
            Expression? predicate = null;

            foreach (var prop in keyProperties)
            {
                // e.Id
                var left = Expression.Property(parameter, prop);
                // entity.Id (value)
                var right = Expression.Constant(prop.GetValue(entity), prop.PropertyType);

                var comparison = Expression.Equal(left, right);
                predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate!, parameter);
            return lambda;
        }



        public static Expression<Func<TEntity, bool>> Predicate<TEntity>(this Expression<Func<TEntity, TEntity>> expression)
            where TEntity : Entity<TEntity>
        {
            var type = typeof(TEntity);
            var parameter = Expression.Parameter(type, "e");

            // Get the keys (Reflection logic you already have)
            var keyProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<KeyAttribute>() != null).ToList();
            if (keyProps.Count == 0)
            {
                var idProp = type.GetProperty("Id") ?? type.GetProperty($"{type.Name}Id");
                if (idProp != null) keyProps.Add(idProp);
            }

            // Parse the MemberInitExpression
            if (expression.Body is not MemberInitExpression mi)
                throw new InvalidOperationException("Save expression must be a MemberInit (e.g., e => new Entity { ... })");

            Expression? predicate = null;

            foreach (var binding in mi.Bindings.OfType<MemberAssignment>())
            {
                if (keyProps.Any(k => k.Name == binding.Member.Name))
                {
                    var left = Expression.Property(parameter, binding.Member.Name);
                    // Compile only the value part of the binding to get the ID
                    var value = Expression.Lambda(binding.Expression).Compile().DynamicInvoke();
                    var right = Expression.Constant(value, ((PropertyInfo)binding.Member).PropertyType);

                    var comparison = Expression.Equal(left, right);
                    predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
                }
            }

            // Fallback to fingerprint if no keys were found in the expression bindings
            if (predicate == null)
            {
                // You would implement a similar loop for CanonicalFingerprint here
                throw new InvalidOperationException("No primary key fields were initialized in the expression.");
            }

            return Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
        }




    }
}
