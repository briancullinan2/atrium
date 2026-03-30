using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace DataLayer.Utilities.Extensions
{
    public static class IEntityExtensions
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

        public static async Task<IEntity?> Update(this IEntity entity, IQueryManager? query = null)
        {
            if (entity == null)
            {
                return default!;
            }
            var Query = query ?? entity.QueryManager
                ?? throw new InvalidOperationException("No service provider.");

            return await Query.Update(Query.EphemeralStorage, entity);
        }


        public static async Task<T> Update<T>(this T entity, IQueryManager? query = null) where T : Entity<T>, IEntity
        {
            var Query = query ?? entity.QueryManager
                ?? throw new InvalidOperationException("No service provider.");
            return await Query.Update<T>(Query.EphemeralStorage, entity);
        }

        /// <summary>
        /// Rehydrates the entity by discarding local changes and fetching 
        /// the latest data from the database.
        /// </summary>
        public static async Task<T> Update<T>(this T? entity) where T : Entity<T>, IEntity
        {
            if (entity == null)
            {
                return default!;
            }
            var Query = entity.QueryManager
                ?? throw new InvalidOperationException("No service provider.");

            return await Query.Update(Query.EphemeralStorage, entity);
        }

        /*
        public static async Task<IEntity> Update(this IEntity? entity)
        {
            if (entity == null)
            {
                return default!;
            }
            if (QueryManager.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }

            return await QueryManager.Service.GetRequiredService<IQueryManager>().Update(false, entity);
        }
        */

        /*
        public static async Task<IEntity> Save(this IEntity? ent)
        {
            if (ent == null)
            {
                return default!;
            }
            if (QueryManager.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }

            return await QueryManager.Service.GetRequiredService<IQueryManager>().Save(false, ent);
        }
        */

        public static T Create<T>(this TranslationContext context) where T : Entity<T>, IEntity<T>, IEntity
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

        /*
        public static async Task<T> Save<T>(this T? ent) where T : Entity<T>, IEntity<T>, IEntity
        {
            if (ent == null)
            {
                return default!;
            }
            var Query = ent.QueryManager
                ?? QueryManager.Service?.GetService(typeof(IQueryManager)) as IQueryManager
                ?? throw new InvalidOperationException("No service provider.");
            return await Query.Save(Query.EphemeralStorage, ent);
        }
        */

        public static async Task<T> Save<T>(this T? ent, IServiceProvider? Service = null) where T : Entity<T>, IEntity<T>, IEntity
        {
            if (ent == null)
            {
                return default!;
            }
            var Query = Service?.GetService<IQueryManager>() ?? ent.QueryManager
                ?? throw new InvalidOperationException("No query manager.");
            return await Query.Save(Query.EphemeralStorage, ent);
        }

        public static async Task<T> Save<T>(this T? ent, IQueryManager? query = null) where T : Entity<T>, IEntity<T>, IEntity
        {
            if (ent == null)
            {
                return default!;
            }
            var Query = query ?? ent.QueryManager ?? throw new InvalidOperationException("No query manager.");
            return await Query.Save(Query.EphemeralStorage, ent);
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
        /*
        public static async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(Expression<Func<TSet, bool>> qualifier)
            where TFrom : TranslationContext
            where TTo : TranslationContext
            where TSet : Entity<TSet>
        {
            if (contextFrom.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            var Query = contextFrom.Service.GetRequiredService<IQueryManager>();
            return await Query.Synchronize(contextFrom, contextTo, qualifier);
        }
        */

        public static async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(this TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier)
            where TFrom : TranslationContext
            where TTo : TranslationContext
            where TSet : Entity<TSet>
        {
            if (contextFrom.Query == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            // TODO: contextTO?
            return await contextFrom.Query.Synchronize(contextFrom, contextTo, qualifier);
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


        public static List<PropertyInfo> Database(this Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    //p.GetGetMethod()?.IsVirtual != true
                    !Attribute.IsDefined(p, typeof(KeyAttribute))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute)))
                .OrderBy(p => p.Name)
                .ToList();

            return properties;
        }



        public static List<PropertyInfo> Predicate(this Type type)
        {
            var primaryKey = type.GetCustomAttribute<PrimaryKeyAttribute>()?.PropertyNames;
            var keyProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<KeyAttribute>() != null || primaryKey?.Contains(p.Name) == true)
                .ToList();
            return keyProperties;
        }


        public static Dictionary<string, List<PropertyInfo>> Indexes(this Type type)
        {
            var indexes = type.GetCustomAttributes<IndexAttribute>()
                .ToDictionary<IndexAttribute, string, List<PropertyInfo>>(
                    i => i.Name ?? string.Empty,
                    i => [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => i.PropertyNames.Contains(p.Name))]);
            return indexes;
        }


        public static List<PropertyInfo> Interesting(this Type type)
        {
            var foreignKeys = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => Attribute.GetCustomAttribute(p, typeof(ForeignKeyAttribute))?.TypeId);

            // Get properties that are NOT virtual (Nav properties) and NOT marked [NotMapped]
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    !foreignKeys.Contains(p.Name) // TODO: skip all FK IDs because they might not match on server
                                                  //&& p.GetGetMethod()?.IsVirtual != true
                    && !Attribute.IsDefined(p, typeof(KeyAttribute))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    && !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute))
                    && !Attribute.IsDefined(p, typeof(ForeignKeyAttribute)) // comparing id is enough
                    && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType))
                .OrderBy(p => p.Name)
                .ToList();

            return properties;
        }

        public static List<PropertyInfo> Display(this Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    (Attribute.IsDefined(p, typeof(CategoryAttribute))
                    || Attribute.IsDefined(p, typeof(DisplayAttribute)))
                    && (string.Equals(p.GetCustomAttribute<CategoryAttribute>()?.Category, "Display")
                    || string.Equals(p.GetCustomAttribute<DisplayAttribute>()?.GroupName, "Display"))
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    && !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute))

                    )
                .OrderBy(p => p.Name)
                .ToList();

            return properties;
        }



        public static List<(string Name, Type EntityType)> Schemas(DbContext context)
        {
            return context.GetType().Schemas();
        }


        public static List<(string Name, Type EntityType)> Schemas(this Type contextType)
        {
            if (contextType.Extends(typeof(TranslationContext)))
                return [.. (contextType.GetProperties(nameof(TranslationContext.EntityTypes))
                    .FirstOrDefault()?.GetValue(null) as List<Type>)
                    ?.Select(p => (
                        Name: p.Table() ?? p.Name,
                        EntityType: p
                    )) ?? []];


            List<(string Name, Type EntityType)> sets = [.. contextType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .Select(p => (
                    Name: p.PropertyType.GetGenericArguments()[0].Table() ?? p.Name,
                    EntityType: p.PropertyType.GetGenericArguments()[0]
                ))];

            if(sets.Count != 0) return sets;

            if (!contextType.Extends(typeof(TranslationContext)))
                throw new InvalidOperationException("Not sure what to do here, type is not a TranslationContext");

            throw new InvalidOperationException("Not sure what to do here, how to get a list of DbSets<>");
        }


    }
}
