using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace DataLayer.Utilities.Extensions
{
    public static class IEntityExtensions
    {
        /// <summary>
        /// Rehydrates the entity by discarding local changes and fetching 
        /// the latest data from the database.
        /// </summary>
        public static async Task<T> Update<T>(this T entity) where T : Entity<T>
        {
            if (QueryManager.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }

            return await QueryManager.Service.GetRequiredService<QueryManager>().Update(false, entity);
        }





        public static async Task<T?> Save<T>(this T? ent) where T : Entity<T>
        {
            if(ent == null)
            {
                return default!;
            }
            if (QueryManager.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }

            return await QueryManager.Service.GetRequiredService<QueryManager>().Save(false, ent);
        }

        //public static T Wrap<T>(this T target) where T : class, IEntity<T>
        //{
        //    return T.Wrap(target, target._service);
        //}


        /*
        public static List<T> ToList<T>(this IQueryable<T> query) where T : IEntity, new()
        {
            // 1. Convert the IQueryable to a raw SQL string
            // If using EF Core: string sql = query.ToQueryString();
            // If using a custom metadata provider, call its specific SQL generator
            string sql = query.ToString();

            var results = new List<T>();

            // 2. Execute via ADO.NET for high-performance medical-grade streaming
            using (var conn = DataFactory.CreateConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    conn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Use your reflection-based mapper to hydrate the entity
                            results.Add(DataMapper.Map<T>(reader));
                        }
                    }
                }
            }
            return results;
        }
        */

        public static async Task Sync<TFrom, TTo, TSet>(this TFrom memoryContext, TTo persistentContext, Expression<Func<TSet, bool>> qualifier)
            where TFrom : TranslationContext
            where TTo : TranslationContext
            where TSet : Entity<TSet>
        {
            // 1. Get the "Dirty" or all entities from memory
            var entities = await memoryContext.Set<TSet>().AsNoTracking().Where(qualifier).ToListAsync();

            foreach (var entity in entities)
            {
                // 2. Upsert logic: Check if it exists in the persistent store
                var exists = persistentContext.EntryAsync(entity);

                if (exists != null)
                {
                    _ = persistentContext.Set<TSet>().Update(entity);
                }
                else
                {
                    _ = persistentContext.Set<TSet>().Add(entity);
                }
            }

            _ = await persistentContext.SaveChangesAsync();
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
            var keyProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<KeyAttribute>() != null)
                .ToList();

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



        public static async Task<TEntity?> EntryAsync<TEntity>(this DbContext context, TEntity entity)
            where TEntity : Entity<TEntity>
        {
            var lambda = Predicate(entity);
            return await context.Set<TEntity>().FirstOrDefaultAsync(lambda);
        }



        public static async Task<bool> ExistsAsync<TEntity>(this DbContext context, TEntity entity)
            where TEntity : Entity<TEntity>
        {
            return (await EntryAsync(context, entity)) != null;
        }

    }
}
