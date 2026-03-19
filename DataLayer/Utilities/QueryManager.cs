using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Reflection;

namespace DataLayer.Utilities
{

    public interface IQueryManager
    {
        // TODO: for overriding in web client to switch persistent to remote, code reduction
        StorageType EphemeralStorage { get; set; }
        StorageType PersistentStorage { get; set; }
        Type EphemeralType { get; set; }
        Type PersistentType { get; set; }
        Type? EphemeralContext { get; set; }
        Type? PersistentContext { get; set; }


        Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;
        Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;
        Task<List<TSet>> Synchronize<TSet>(bool FromPersistent, bool ToPersistent, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;


        Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Save<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


        Task<IQueryable<TEntity>> Query<TEntity>(Expression<Func<TEntity, bool>>? query = null, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(bool persistent, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;


        Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        //Task<object?> Update(bool persistent, object key, int priority = 10);
        Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


        Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


        Task ProcessQueueAsync();




        Type GetStorageType(StorageType type);

        Type GetContextType(StorageType type);

        Type? GetContextType(Type? type);



        IDbContextFactory<TContext>? GetContextFactory<TContext>() where TContext : DbContext;

        IDbContextFactory<TContext>? GetContextFactory<TContext>(Type contextType) where TContext : DbContext;



        TContext? GetContext<TContext>() where TContext : DbContext;

        TContext? GetContext<TContext>(Type contextType) where TContext : DbContext;



        TranslationContext? GetContext(Type contextType);

        TranslationContext? GetContext(StorageType type);

    }


    
    public class QueryManager : IQueryManager
    {
        public static IServiceProvider? Service { get; set; } = null;
        // Priority 0 = High (UI updates), 10 = Low (Background sync)
        protected static PriorityQueue<TaskCompletionSource, int> TaskQueue { get; } = new();
        private static readonly SemaphoreSlim _processorLock = new(1, 1);
        private static readonly SemaphoreSlim _gate = new(0);

        public virtual StorageType EphemeralStorage { get; set; } = StorageType.Ephemeral;
        public virtual StorageType PersistentStorage { get; set; } = StorageType.Persistent;
        private Type? _ephemeral;
        private Type? _persistent;
        public virtual Type EphemeralType {
            get => _ephemeral ?? GetStorageType(EphemeralStorage);
            set => _ephemeral = value; 
        }
        public virtual Type PersistentType {
            get => _persistent ?? GetStorageType(PersistentStorage);
            set => _persistent = value;
        }
        public virtual Type? EphemeralContext {
            get => GetContextType(_ephemeral) ?? GetContextType(EphemeralType);
            set => _ephemeral = value?.GetType().GetGenericArguments()[0];
        }
        public virtual Type? PersistentContext {
            get => GetContextType(_ephemeral) ?? GetContextType(PersistentType);
            set => _persistent = value?.GetType().GetGenericArguments()[0];
        }


        public QueryManager()
        {

        }


        public static async Task<object?> DetypeTask<TReturn>(Task<TReturn> Callback)
        {
            var result = await Callback;
            return result;
        }



        public async Task<TReturn> Enqueue<TReturn>(Func<Task<TReturn>> callback, int priority = 5)
        {
            var myTurn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (TaskQueue)
            {
                TaskQueue.Enqueue(myTurn, priority);
            }

            // Kick the worker
            _ = ProcessQueueAsync();

            // Wait for the background loop to tell us it's our turn
            await myTurn.Task;

            try
            {
                var result = await callback();

                if (result is IQueryable queryable)
                {
                    // Use reflection or 'dynamic' to call ToList() 
                    // This pulls the data into memory while the DbContext is still alive
                    var list = Enumerable.ToList((dynamic)queryable);
                    var finalResult = (TReturn)Queryable.AsQueryable(list);
                    var finalType = typeof(TReturn);
                    return (TReturn)finalResult;
                }

                return result;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                throw new Exception("holy shit", ex);
            }
            finally
            {
                // Tell the background loop we are done so it can release the next one
                _ = _gate.Release();
            }
        }



        public virtual async Task ProcessQueueAsync()
        {
            // Ensure only one worker loop runs
            if (!await _processorLock.WaitAsync(0)) return;

            try
            {
                while (true)
                {
                    TaskCompletionSource? next;
                    lock (TaskQueue)
                    {
                        if (!TaskQueue.TryDequeue(out next, out _)) break;
                    }

                    // Important: Let the thread pool breathe
                    await Task.Yield();

                    // 1. Give the caller their turn
                    next.TrySetResult();

                    // 2. WAIT for the caller to finish their finally block
                    // This ensures the DbContext in the callback is fully disposed 
                    // before the next loop starts.
                    await _gate.WaitAsync();
                }
            }
            finally
            {
                _processorLock.Release();
            }
        }




        public Type GetStorageType(StorageType type) => type switch
        {
            StorageType.Ephemeral => typeof(EphemeralStorage),
            StorageType.Persistent => typeof(PersistentStorage),
            StorageType.Remote => typeof(RemoteStorage),
            StorageType.Test => typeof(TestStorage),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
        };


        public Type GetContextType(StorageType type) => type switch
        {
            StorageType.Ephemeral => typeof(IDbContextFactory<EphemeralStorage>),
            StorageType.Persistent => typeof(IDbContextFactory<PersistentStorage>),
            StorageType.Remote => typeof(IDbContextFactory<RemoteStorage>),
            StorageType.Test => typeof(IDbContextFactory<TestStorage>),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
        };


        public Type? GetContextType(Type? type)
        {
            if (type == null) return null;
            return typeof(IDbContextFactory<>).MakeGenericType(type);
        }


        public IDbContextFactory<TContext>? GetContextFactory<TContext>() where TContext : DbContext => 
            Service?.GetService<IDbContextFactory<TContext>>();

        public IDbContextFactory<TContext>? GetContextFactory<TContext>(Type contextType) where TContext : DbContext =>
            Service?.GetService(contextType) as IDbContextFactory<TContext>;


        public TContext? GetContext<TContext>() where TContext : DbContext =>
            GetContextFactory<TContext>()?.CreateDbContext();

        public TContext? GetContext<TContext>(Type contextType) where TContext : DbContext =>
            GetContextFactory<TContext>(contextType)?.CreateDbContext();

        public TranslationContext? GetContext(Type contextType) =>
            typeof(QueryManager).GetMethod(nameof(GetContext), 1, [typeof(Type)])?.Invoke(null, [contextType]) as TranslationContext;

        public TranslationContext? GetContext(StorageType type)
        {
            var contextType = GetStorageType(type);
            var factoryType = GetContextType(contextType);
            var contextMethod = typeof(QueryManager)
                .GetMethod(nameof(GetContext), 1, [typeof(Type)])
                ?.MakeGenericMethod(contextType);
            try
            {
                return contextMethod?.Invoke(this, [factoryType]) as TranslationContext;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }



        public async Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return await Synchronize(true, false, qualifier, priority);
        }

        public virtual async Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return await Enqueue(async () =>
            {
                using var scope = Service?.CreateScope();
                var contextFrom = GetContext(From);
                var contextTo = GetContext(To);
                if (contextFrom == null || contextTo == null)
                {
                    throw new InvalidOperationException("Database context failed.");
                }

                var entities = await contextFrom.Set<TSet>().AsNoTracking().Where(qualifier).ToListAsync();

                foreach (var entity in entities)
                {
                    var exists = await contextTo.Set<TSet>().AnyAsync(qualifier);

                    if (exists)
                    {
                        _ = contextTo.Set<TSet>().Update(entity);
                    }
                    else
                    {
                        _ = contextTo.Set<TSet>().Add(entity);
                    }
                }

                _ = await contextTo.SaveChangesAsync();

                return await contextTo.Set<TSet>().Where(qualifier).ToListAsync();
            }, priority);
        }

        public async Task<List<TSet>> Synchronize<TSet>(bool FromPersistent, bool ToPersistent, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return await Synchronize(
                FromPersistent ? StorageType.Persistent : StorageType.Ephemeral,
                ToPersistent ? StorageType.Persistent : StorageType.Ephemeral,
                qualifier, priority);
        }


        public async Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(false, expression, priority);
        }

        public async Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(false, entity, priority);
        }




        public void ShallowSaveRecursive<T>(DbContext persistentContext, T updatedEntity, bool recurse = false) where T : class, IEntity<T>
        {
            var trackedEntity = persistentContext.Entry(updatedEntity);
            if (trackedEntity == null)
            {
                // If it doesn't exist, we must Add it (Shallowly)
                _ = persistentContext.Add(updatedEntity);
                return;
            }

            trackedEntity.CurrentValues.SetValues(updatedEntity);

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


        public virtual async Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Enqueue(async () =>
            {
                using var scope = Service?.CreateScope();
                var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");

                var predicate = expression.Predicate();
                var entity = await context.Set<TEntity>().FirstOrDefaultAsync(predicate)
                             ?? Activator.CreateInstance<TEntity>();

                var updates = expression.ToMembers();

                foreach (var update in updates)
                {
                    if (update.Key is PropertyInfo prop && prop.CanWrite)
                    {
                        prop.SetValue(entity, update.Value);
                    }
                }

                if (context.Entry(entity).State == EntityState.Detached)
                    _ = context.Add(entity);

                entity.CanonicalFingerprint = entity.GetHashCode(); // Your fingerprint logic
                _ = await context.SaveChangesAsync();

                return await UpdateNow(storage, entity);
            }, priority);
        }

        public virtual async Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Enqueue(async () =>
            {
                using var scope = Service?.CreateScope();
                var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");
                using var transaction = context.Database.BeginTransaction();
                try
                {
                    entity.CanonicalFingerprint = entity.GetHashCode();

                    _ = context.Add(entity);

                    _ = context.SaveChanges();

                    transaction.Commit();

                    return await UpdateNow(storage, entity);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception("new bs", ex); // Rethrow so the parent catch can handle the fallback
                }
                finally
                {
                }
            }, priority);
        }



        public async Task<TEntity> Save<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(persistent ? StorageType.Persistent : StorageType.Ephemeral, expression, priority);
        }

        public async Task<TEntity> Save<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }





        public async Task<IQueryable<TEntity>> Query<TEntity>(Expression<Func<TEntity, bool>>? query = null, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Query(false, (IQueryable<TEntity> entities) => query != null ? entities.Where(query) : entities, priority);
        }

        public async Task<TResult> Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Query(false, query, priority);
        }


        // TODO: make this only queue once per same result
        private static readonly ConcurrentDictionary<string, Task<object?>> _pendingQueries = new();

        public virtual async Task<TResult> Query<TEntity, TResult>(
            StorageType storage,
            Expression<Func<IQueryable<TEntity>, TResult>> query,
            int priority = 10)
            where TEntity : Entity<TEntity>
        {
            // 1. Generate a unique key for this specific query signature
            // For "absurd detail," you could use query.ToXDocument() if you've mapped it,
            // but ToString() catches 99% of identical LINQ structures.
            string queryKey = $"{typeof(TEntity).Name}_{typeof(TResult).Name}_{storage}_{query}";

            // 2. Check if this exact query is already "in flight"
            // GetOrAdd ensures that only one Task is created for the same key.
            var task = _pendingQueries.GetOrAdd(queryKey, _ =>
            {
                // This inner block only runs ONCE for the same queryKey
                return Enqueue(async () =>
                {
                    try
                    {
                        using var scope = Service?.CreateScope();
                        var context = GetContext(storage) ?? throw new InvalidOperationException("DB context failed.");

                        IQueryable<TEntity> set = context.Set<TEntity>().AsQueryable();
                        var compiledQuery = query.Compile();
                        TResult result = compiledQuery(set);

                        if (result is Task taskResult)
                        {
                            await taskResult;
                            return (object?)((dynamic)taskResult).Result;
                        }

                        return (object?)result;
                    }
                    finally
                    {
                        // 3. CRITICAL: Remove the task from the dictionary when done
                        // so subsequent calls actually hit the DB for fresh data.
                        _pendingQueries.TryRemove(queryKey, out var _);
                    }
                }, priority);
            });

            // 4. All callers (original and late-comers) await the same task
            var finalResult = await task;
            var finalType = typeof(TResult);
            return (TResult)finalResult!;
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



        /*
        public int Update<T>(this T entity, IDbConnection conn, string keyName = "Id") where T : class, IEntity<T>
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
        */


        public async Task<TResult> Query<TEntity, TResult>(bool persistent, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Query(persistent ? StorageType.Persistent : StorageType.Ephemeral, query, priority);
        }




        public async Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update(false, key, priority);
        }

        public async Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update(false, entity, priority);
        }



        public async Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> key, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public async Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }



        public virtual async Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Enqueue(async () =>
            {
                using var scope = Service?.CreateScope();
                var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");


                var predicate = key.Predicate();
                var entity = await context.Set<TEntity>().FirstOrDefaultAsync(predicate)
                             ?? Activator.CreateInstance<TEntity>();

                var entry = context.Entry(entity);
                if (entry.State == EntityState.Detached)
                {
                    _ = context.Attach(entity);
                }
                entry.Reload();
                LoadAllNavigations(context, entity);


                // reapply members values
                var updates = key.ToMembers();
                foreach (var update in updates)
                {
                    if (update.Key is PropertyInfo prop && prop.CanWrite)
                    {
                        prop.SetValue(entity, update.Value);
                    }
                }


                return entity;
            }, priority);
        }



        public virtual async Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>

        {
            return await Enqueue(async () =>
            {
                return await UpdateNow(storage, entity);
            }, priority);
        }


        public virtual async Task<TEntity> UpdateNow<TEntity>(StorageType storage, TEntity entity)
            where TEntity : Entity<TEntity>

        {
            using var scope = Service?.CreateScope();
            var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");

            if (entity == null)
            {
                throw new InvalidOperationException("Entity is null.");
            }
            var entry = context.Entry(entity);

            // If the entity isn't being tracked, we need to attach it first
            if (entry.State == EntityState.Detached)
            {
                _ = context.Attach(entity);
            }

            // This executes the SQL SELECT and updates the object's properties
            entry.Reload();
            LoadAllNavigations(context, entity);

            return entity;
        }
    }


    public class RemoteManager : QueryManager
    {
        private readonly HttpClient? _httpClient;

        public RemoteManager()
        {
            PersistentStorage = StorageType.Test;
            PersistentStorage = StorageType.Remote;
            _httpClient = Service?.GetRequiredService<HttpClient>();
        }



    }

}
