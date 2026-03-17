using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Data;
using System.Linq.Expressions;

namespace DataLayer.Utilities.Extensions
{
    public static class IEntityExtensions
    {
        /// <summary>
        /// Rehydrates the entity by discarding local changes and fetching 
        /// the latest data from the database.
        /// </summary>
        public static T Update<T>(this ProxyEntity<T> entity, bool? recurse = false) where T : class, IEntity<T>
        {
            ArgumentNullException.ThrowIfNull(entity);
            entity._scope ??= entity._service?.CreateScope();
            var persistentStore = entity._scope?.ServiceProvider.GetRequiredService(entity._context ?? typeof(IDbContextFactory<PersistentStorage>));
            TranslationContext? persistentContext = (persistentStore as IDbContextFactory<PersistentStorage>)?.CreateDbContext();
            persistentContext ??= (persistentStore as IDbContextFactory<EphemeralStorage>)?.CreateDbContext();
            if (persistentContext == null)
            {
                throw new InvalidOperationException("Cannot determine database context.");
            }
            var entry = persistentContext.Entry(entity._target);

            // If the entity isn't being tracked, we need to attach it first
            if (entry.State == EntityState.Detached)
            {
                _ = persistentContext.Attach(entity._target);
            }

            // This executes the SQL SELECT and updates the object's properties
            entry.Reload();

            if (recurse == true)
            {
                LoadAllNavigations(persistentContext, entity._target);
            }

            return entity._target;
        }


        public static void LoadAllNavigations(DbContext context, object entity)
        {
            var entry = context.Entry(entity);

            // 1. Get all Navigation properties defined in the EF Model for this type
            var navigations = entry.Metadata.GetNavigations();

            foreach (var navigation in navigations)
            {
                if (navigation.IsCollection)
                {
                    // It's a Collection (like ICollection<Lesson>)
                    var collectionEntry = entry.Collection(navigation.Name);
                    if (!collectionEntry.IsLoaded)
                    {
                        collectionEntry.Load();
                    }
                }
                else
                {
                    // It's a Reference (like ParentLesson)
                    var referenceEntry = entry.Reference(navigation.Name);
                    if (!referenceEntry.IsLoaded)
                    {
                        referenceEntry.Load();
                    }
                }
            }
        }


        public static T Save<T>(this ProxyEntity<T> ent, bool? recurse = false) where T : class, IEntity<T>
        {
            // Start the Transaction
            ent._scope ??= ent._service?.CreateScope();
            var persistentStore = ent._scope?.ServiceProvider.GetRequiredService(ent._context ?? typeof(IDbContextFactory<PersistentStorage>));
            TranslationContext? persistentContext = (persistentStore as IDbContextFactory<PersistentStorage>)?.CreateDbContext();
            persistentContext ??= (persistentStore as IDbContextFactory<EphemeralStorage>)?.CreateDbContext();
            if (persistentContext == null)
            {
                throw new InvalidOperationException("Cannot determine database context.");
            }
            using var transaction = persistentContext.Database.BeginTransaction();
            try
            {
                // 1. Perform relational checks here (e.g., does the linked Facility exist?)
                // if (!context.Facilities.Any(f => f.Id == messageEntity.FacilityId)) 
                //    throw new Exception("Invalid Facility Link");
                ent._target.CanonicalFingerprint = ent._target.GetHashCode();

                // 2. Add the primary entity
                if (recurse == false)
                {
                    ShallowSaveRecursive(persistentContext, ent._target, recurse == true);
                }
                else
                {
                    _ = persistentContext.Add(ent._target);
                }

                // 3. Commit the changes
                _ = persistentContext.SaveChanges();

                // 4. Finalize the transaction
                transaction.Commit();

                return Update(ent, true);
            }
            catch (Exception ex)
            {
                // Arizona Compliance: Roll back to prevent data corruption
                transaction.Rollback();
                //Log.Error($"Transaction Aborted: {ex.Message}");
                throw new Exception("new bs", ex); // Rethrow so the parent catch can handle the fallback
            }
            finally
            {
            }
        }

        public static void ShallowSaveRecursive<T>(DbContext persistentContext, T updatedEntity, bool recurse = false) where T : class, IEntity<T>
        {

            // 2. Find or Fetch the tracked version from the DB
            var trackedEntity = persistentContext.Entry(updatedEntity);
            if (trackedEntity == null)
            {
                // If it doesn't exist, we must Add it (Shallowly)
                _ = persistentContext.Add(updatedEntity);
                return;
            }

            // 3. Update only the scalar values (Title, Icon, etc.)
            trackedEntity.CurrentValues.SetValues(updatedEntity);

            // 4. If recursing, find child collections
            if (recurse)
            {
                var navigations = persistentContext.Entry(trackedEntity).Metadata.GetNavigations();
                foreach (var nav in navigations.Where(n => n.IsCollection))
                {
                    // Get the list of children from the updated object

                    if (updatedEntity.GetType().GetProperty(nav.Name)?.GetValue(updatedEntity) is IEnumerable updatedChildren)
                    {
                        foreach (var child in updatedChildren)
                        {
                            // Use 'dynamic' or Reflection to call this method again for the child type
                            ShallowSaveRecursive((dynamic)child, true);
                        }
                    }
                }
            }
        }

        public static int Update<T>(this T entity, IDbConnection conn, string keyName = "Id") where T : class, IEntity<T>
        {
            var type = typeof(T);
            var props = type.GetProperties();
            var tableName = type.Name; // Assumes Table Name = Class Name

            // 1. Build the SET clause (skipping the Primary Key)
            var setClauses = props
                .Where(p => p.Name != keyName)
                .Select(p => $"[{p.Name}] = @{p.Name}");

            string sql = $"UPDATE [{tableName}] SET {string.Join(", ", setClauses)} WHERE [{keyName}] = @{keyName}";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            // 2. Map values to Parameters (Prevents SQL Injection)
            foreach (var prop in props)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(entity) ?? DBNull.Value;
                _ = cmd.Parameters.Add(param);
            }

            if (conn.State != ConnectionState.Open) conn.Open();
            return cmd.ExecuteNonQuery();
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
                var exists = await persistentContext.Set<TSet>().AnyAsync(qualifier);

                if (exists)
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

    }
}
