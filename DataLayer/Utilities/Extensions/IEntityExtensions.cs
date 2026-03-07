using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        public static void Refetch<T>(this ProxyEntity<T> entity) where T : IEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            using var scope = entity._service.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TranslationContext>();
            var entry = context.Entry(entity);

            // If the entity isn't being tracked, we need to attach it first
            if (entry.State == EntityState.Detached)
            {
                context.Attach(entity);
            }

            // This executes the SQL SELECT and updates the object's properties
            entry.Reload();
        }

        public static void Save<T>(this ProxyEntity<T> ent, bool? recurse = false) where T : class, IEntity<T>
        {
            // Start the Transaction
            using var scope = ent._service?.CreateScope();
            var persistentStore = scope?.ServiceProvider.GetRequiredService(ent._context ?? typeof(IDbContextFactory<DataLayer.PersistentStorage>));
            TranslationContext? persistentContext = (persistentStore as IDbContextFactory<DataLayer.PersistentStorage>)?.CreateDbContext();
            if (persistentContext == null)
            {
                persistentContext = (persistentStore as IDbContextFactory<DataLayer.EphemeralStorage>)?.CreateDbContext();
            }
            if (persistentContext == null)
            {
                throw new InvalidOperationException("Cannot determine database context.");
            }
            using (var transaction = persistentContext.Database.BeginTransaction())
            {
                try
                {
                    // 1. Perform relational checks here (e.g., does the linked Facility exist?)
                    // if (!context.Facilities.Any(f => f.Id == messageEntity.FacilityId)) 
                    //    throw new Exception("Invalid Facility Link");

                    // 2. Add the primary entity
                    persistentContext.Set<T>().Add(ent._target);

                    // 3. Commit the changes
                    persistentContext.SaveChanges();

                    // 4. Finalize the transaction
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    // Arizona Compliance: Roll back to prevent data corruption
                    transaction.Rollback();
                    //Log.Error($"Transaction Aborted: {ex.Message}");
                    throw; // Rethrow so the parent catch can handle the fallback
                }
            }
        }

        public static int Update<T>(this T entity, IDbConnection conn, string keyName = "Id") where T : IEntity<T>
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
                cmd.Parameters.Add(param);
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
                    persistentContext.Set<TSet>().Update(entity);
                }
                else
                {
                    persistentContext.Set<TSet>().Add(entity);
                }
            }

            await persistentContext.SaveChangesAsync();
        }

    }
}
