using DataLayer.Entities;
using DataLayer.Generators;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

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
        
        
        //Task<IEntity> Save(bool persistent, IEntity entity, int priority = 10);


        Task<IQueryable<TEntity>> Query<TEntity>(Expression<Func<TEntity, bool>>? query = null, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(bool persistent, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;



        Task<TEntity> Update<TEntity, TResult>(StorageType storage, Expression<Func<TEntity, TResult>> key, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Update<TEntity>(Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        //Task<object?> Update(bool persistent, object key, int priority = 10);
        Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


        Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


        Task ProcessQueueAsync();



        StorageType GetStorageType(Type type);
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
        public virtual Type EphemeralType
        {
            get => _ephemeral ?? GetStorageType(EphemeralStorage);
            set => _ephemeral = value;
        }
        public virtual Type PersistentType
        {
            get => _persistent ?? GetStorageType(PersistentStorage);
            set => _persistent = value;
        }
        public virtual Type? EphemeralContext
        {
            get => GetContextType(_ephemeral) ?? GetContextType(EphemeralType);
            set => _ephemeral = value?.GetType().GetGenericArguments()[0];
        }
        public virtual Type? PersistentContext
        {
            get => GetContextType(_ephemeral) ?? GetContextType(PersistentType);
            set => _persistent = value?.GetType().GetGenericArguments()[0];
        }
        public static MethodInfo UpdateGeneric { get; }

        static QueryManager()
        {

            var methods = typeof(QueryManager)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            UpdateGeneric = methods
                .FirstOrDefault(m => m.Name == nameof(UpdateNow) && m.IsGenericMethod && m.GetParameters().First().ParameterType == typeof(DbContext))
                ?? throw new Exception("UpdateNow method definition not found.");
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
            catch (Exception ex)
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




        public Type GetStorageType(StorageType type)
        {
            return type switch
            {
                StorageType.Ephemeral => typeof(EphemeralStorage),
                StorageType.Persistent => typeof(PersistentStorage),
                StorageType.Remote => typeof(RemoteStorage),
                StorageType.Test => typeof(TestStorage),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
            };
        }



        public StorageType GetStorageType(Type type)
        {
            if (typeof(IDbContextFactory<EphemeralStorage>).IsAssignableFrom(type) || type == typeof(EphemeralStorage))
                return StorageType.Ephemeral;
            if (typeof(IDbContextFactory<PersistentStorage>).IsAssignableFrom(type) || type == typeof(PersistentStorage))
                return StorageType.Persistent;
            if (typeof(IDbContextFactory<TestStorage>).IsAssignableFrom(type) || type == typeof(TestStorage))
                return StorageType.Test;
            if (typeof(IDbContextFactory<RemoteStorage>).IsAssignableFrom(type) || type == typeof(RemoteStorage))
                return StorageType.Remote;
            throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.");
        }

        public Type GetContextType(StorageType type)
        {
            return type switch
            {
                StorageType.Ephemeral => typeof(IDbContextFactory<EphemeralStorage>),
                StorageType.Persistent => typeof(IDbContextFactory<PersistentStorage>),
                StorageType.Remote => typeof(IDbContextFactory<RemoteStorage>),
                StorageType.Test => typeof(IDbContextFactory<TestStorage>),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
            };
        }

        public Type? GetContextType(Type? type)
        {
            if (type == null) return null;
            return typeof(IDbContextFactory<>).MakeGenericType(type);
        }


        public IDbContextFactory<TContext>? GetContextFactory<TContext>() where TContext : DbContext
        {
            return Service?.GetService<IDbContextFactory<TContext>>();
        }

        public IDbContextFactory<TContext>? GetContextFactory<TContext>(Type contextType) where TContext : DbContext
        {
            return Service?.GetService(contextType) as IDbContextFactory<TContext>;
        }

        public TContext? GetContext<TContext>() where TContext : DbContext
        {
            return GetContextFactory<TContext>()?.CreateDbContext();
        }

        public TContext? GetContext<TContext>(Type contextType) where TContext : DbContext
        {
            return GetContextFactory<TContext>(contextType)?.CreateDbContext();
        }

        public TranslationContext? GetContext(Type contextType)
        {
            return typeof(QueryManager).GetMethod(nameof(GetContext), 1, [typeof(Type)])?.Invoke(null, [contextType]) as TranslationContext;
        }

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

        public async Task ShallowSaveRecursive<T>(DbContext persistentContext, T updatedEntity, bool recurse = true) where T : Entity<T>
        {
            var compiled = updatedEntity.Predicate().Compile();
            var trackedEntry = persistentContext.ChangeTracker.Entries<T>()
                .FirstOrDefault(e => compiled(e.Entity))
                ?? persistentContext.Entry(updatedEntity);

            if (trackedEntry.Entity == null || trackedEntry.Entity.CanonicalFingerprint == null)
            {
                // This is a NEW user, but we MUST NOT use context.Add(updatedEntity) 
                // because it will try to re-insert the Roles.
                trackedEntry.State = EntityState.Added;
            }

            trackedEntry.CurrentValues.SetValues(updatedEntity);

            if (!recurse)
            {
                return;
            }
            var navigations = trackedEntry.Metadata.GetNavigations()
                    .Concat<INavigationBase>(trackedEntry.Metadata.GetSkipNavigations())
                    .Where(n => n.IsCollection || n is IReadOnlySkipNavigation)
                    .ToList() ?? [];



            foreach (var nav in navigations)
            {
                var trackedCollection = trackedEntry?.Collection(nav.Name);

                // We need to ensure the internal list is loaded so EF can diff it
                if (trackedCollection?.IsLoaded != true) trackedCollection?.Load();
                
                var trackedList = trackedCollection?.CurrentValue as IList;
                if (((nav.GetMemberInfo(false, true) as PropertyInfo)?.GetValue(updatedEntity)
                    ?? (nav.GetMemberInfo(false, true) as FieldInfo)?.GetValue(updatedEntity)) is not IEnumerable incomingList) continue;

                if (!ReferenceEquals(trackedList, incomingList))
                {
                    // Clear the tracked list and fill it with the incoming "Ghosts" 
                    // (which we will resolve in the next step)
                    trackedList?.Clear();
                }

                
                var genericMethod = UpdateGeneric.MakeGenericMethod(nav.TargetEntityType.ClrType);

                var resolvedItems = new List<object>();
                foreach (var child in incomingList.Cast<object>().ToList() ?? Enumerable.Empty<object>())
                {
                    if (genericMethod.Invoke(this, [persistentContext, child, null, 3]) is Task task)
                    {
                        await task;
                        var itemEntry = persistentContext.Entry((task as dynamic).Result);

                        // TODO: this worked for the recursion error so it's probably going to fuck me over in the near future.
                        if (itemEntry.State != EntityState.Unchanged
                            && itemEntry.State != EntityState.Added)
                        {
                            ShallowSaveRecursive(persistentContext, (task as dynamic).Result, recurse);
                        }

                        resolvedItems.Add((task as dynamic).Result);
                        //persistentContext.Add((task as dynamic).Result);
                    }
                }

                var toRemove = trackedList?.Cast<object>().Where(x => !resolvedItems.Contains(x)).ToList() ?? [];
                foreach (var item in toRemove) trackedList?.Remove(item);

                // Add what's new
                foreach (var item in resolvedItems)
                {
                    if (trackedList?.Contains(item) != true) trackedList?.Add(item);
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

                return await SaveNow(storage, entity);
            }, priority);
        }


        public virtual async Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Enqueue(async () =>
            {
                return await SaveNow(storage, entity);
            }, priority);
        }


        public virtual async Task<TEntity> SaveNow<TEntity>(StorageType storage, TEntity entity) where TEntity : Entity<TEntity>
        {
            using var scope = Service?.CreateScope();
            var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");
            using var transaction = context.Database.BeginTransaction();
            try
            {

                await ShallowSaveRecursive(context, entity);
                entity.CanonicalFingerprint = entity.GetHashCode();
                _ = await context.SaveChangesAsync();

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception("new bs", ex); // Rethrow so the parent catch can handle the fallback
            }

            return await UpdateNow(storage, entity);
        }

        /*
        public async Task<IEntity> Save(StorageType storage, IEntity entity, int priority = 10)
        {
            return await Enqueue(async () =>
            {
                using var scope = Service?.CreateScope();
                var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");
                using var transaction = context.Database.BeginTransaction();
                try
                {
                    entity.CanonicalFingerprint = entity.GetHashCode();

                    ShallowSaveRecursive(context, entity);

                    _ = context.SaveChanges();

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception("new bs", ex); // Rethrow so the parent catch can handle the fallback
                }

                return await UpdateNow(storage, entity);

            }, priority);
        }
        */


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
            var variables = query.GetHashCode().ToString();
            try
            {
                variables = JsonSerializer.Serialize(query.ToDictionary()).ToSafe();
            } catch { }
            string queryKey = $"{typeof(TEntity).Name}_{typeof(TResult).Name}_{storage}_{query}_{variables}";

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
                        var invokedExpression = Expression.Invoke(query, set.Expression);
                        TResult result;

                        if (typeof(IEnumerable).IsAssignableFrom(typeof(TResult)) && typeof(TResult) != typeof(string))
                        {
                            // It's a sequence - force materialization to avoid SingleQueryingEnumerable leaks
                            var finalQueryable = set.Provider.CreateQuery(invokedExpression);

                            // Force ToList to materialize it before the context is disposed
                            var forcedList = typeof(Enumerable)
                                .GetMethod(nameof(Enumerable.ToList))
                                ?.MakeGenericMethod(typeof(TEntity)) // Or the target type
                                .Invoke(null, [finalQueryable])!;

                            if(typeof(IQueryable).IsAssignableFrom(typeof(TResult)))
                            {
                                result = (TResult)Queryable.AsQueryable((IEnumerable)forcedList)!;
                            }
                            else
                            {
                                result = (TResult)forcedList;
                            }
                        }
                        else
                        {
                            result = (TResult)set?.Provider.Execute(invokedExpression)!;
                        }

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


        public async Task LoadAllNavigations<TEntity>(DbContext context, TEntity entity, Expression<Func<TEntity, bool>>? predicate = null, int depth = 3)
            where TEntity : Entity<TEntity>

        {
            if (depth <= 0) return;

            _ = predicate ?? entity.Predicate();

            var entry = context.Entry(entity);
            var navigations = entry.Metadata.GetNavigations()
                .Concat<INavigationBase>(entry.Metadata.GetSkipNavigations());

            foreach (var navigation in navigations)
            {
                // 1. Get the current value (even if it's just a 'New' object with an ID)
                var navValue = navigation.GetGetter().GetClrValue(entity);

                if ((navigation.IsCollection || navigation is IReadOnlySkipNavigation) && navValue is IEnumerable collection)
                {
                    var resolvedItems = new List<object>();
                    var genericMethod = UpdateGeneric.MakeGenericMethod(navigation.TargetEntityType.ClrType);
                    foreach (var item in collection)
                    {
                        // This is where the recursive magic happens
                        // We need a non-generic way to call UpdateNow or a similar resolver
                        if (genericMethod.Invoke(this, [context, item, null, depth]) is Task task)
                        {
                            await task;
                            if ((task as dynamic).Result != null)
                            {
                                resolvedItems.Add((task as dynamic).Result);
                            }
                        }
                    }

                    // Cast to the non-generic IList

                    if (navValue is IList list)
                    {
                        list.Clear();
                        foreach (var resolved in resolvedItems)
                        {
                            // IList.Add(object) handles the internal casting to Role for you
                            list.Add(resolved);
                        }
                    }
                    else
                    {
                        // Fallback for weird collections that don't implement IList
                        ((dynamic)navValue).Clear();
                        foreach (var resolved in resolvedItems)
                        {
                            ((dynamic)navValue).Add((dynamic)resolved); // Force the binder to see the actual type
                        }
                    }
                }
                else if (navValue != null)
                {
                    var genericMethod = UpdateGeneric.MakeGenericMethod(navigation.TargetEntityType.ClrType);
                    var member = navigation.GetMemberInfo(false, true);

                    // 3. Invoke it (Note: You must pass all 4 arguments because Reflection doesn't 'see' defaults)
                    if (genericMethod.Invoke(this, [context, navValue, null, depth]) is Task task)
                    {
                        await task;

                        if (member is PropertyInfo prop)
                        {
                            prop.SetValue(entity, (task as dynamic).Result);
                        }
                        else if (member is FieldInfo field)
                        {
                            field.SetValue(entity, (task as dynamic).Result);
                        }
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
            return await Update<TEntity, TEntity>(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public async Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }

        public async Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, bool>> key, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            var entity = Activator.CreateInstance<TEntity>();
            var updates = key.ToMembers();
            foreach (var update in updates)
            {
                if (update.Key is PropertyInfo prop && prop.CanWrite)
                {
                    prop.SetValue(entity, update.Value);
                }
            }
            return await Enqueue(async () => { return await UpdateNow(storage, entity); }, priority);
        }


        public async Task<TEntity> Update<TEntity, TResult>(StorageType storage, Expression<Func<TEntity, TResult>> key, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            var entity = Activator.CreateInstance<TEntity>();
            var updates = key.ToMembers();
            foreach (var update in updates)
            {
                if (update.Key is PropertyInfo prop && prop.CanWrite)
                {
                    prop.SetValue(entity, update.Value);
                }
            }
            return await Enqueue(async () => { return await UpdateNow(storage, entity); }, priority);
        }

        public virtual async Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Enqueue(async () => { return await UpdateNow(storage, entity); }, priority);
        }

        public async Task<TEntity> UpdateNow<TEntity>(StorageType storage, TEntity entity)
            where TEntity : Entity<TEntity>

        {
            return await UpdateNow(storage, entity, entity.Predicate());
        }


        public virtual async Task<TEntity> UpdateNow<TEntity>(StorageType storage, TEntity entity, Expression<Func<TEntity, bool>>? predicate = null)
            where TEntity : Entity<TEntity>

        {
            using var scope = Service?.CreateScope();
            var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed.");
            return await UpdateNow(context, entity, predicate, 3);
        }

        public virtual async Task<TEntity> UpdateNow<TEntity>(DbContext context, TEntity entity, Expression<Func<TEntity, bool>>? predicate = null, int depth = 3)
            where TEntity : Entity<TEntity>

        {
            predicate ??= entity.Predicate();
            var compiled = predicate.Compile();

            if (entity == null)
            {
                throw new InvalidOperationException("Entity is null.");
            }

            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var primaryKey = entityType?.FindPrimaryKey();
            var predicateValues = entity.Predicate().ToDictionary();
            if (primaryKey?.Properties.Count != predicateValues.Values.Count)
            {
                throw new InvalidOperationException("Predicate not assigned.");
            }

            // If the entity isn't being tracked, we need to attach it first
            if (context.Entry(entity).State == EntityState.Detached)
            {
                var existingEntity = context.Set<TEntity>().Local.FirstOrDefault(e => compiled(e) == true);

                // TODO: make this an overload that accepts an IEnumberable as an output and iterates over all matches?
                existingEntity ??= context.ChangeTracker.Entries<TEntity>().FirstOrDefault(e => compiled(e.Entity))?.Entity;
                existingEntity ??= context.Set<TEntity>().FirstOrDefault(predicate);
                if (existingEntity != null)
                {
                    return existingEntity;
                }

                await LoadAllNavigations(context, entity, predicate, --depth);

                context.Attach(entity);
            }
            else
            {
                await LoadAllNavigations(context, entity, predicate, --depth);
            }

            // This executes the SQL SELECT and updates the object's properties
            try
            {
                await context.Entry(entity).ReloadAsync();
            }
            catch
            {
                
            }

            return entity;
        }

        public Task<TEntity> Update<TEntity>(Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Update<TEntity>(StorageType.Ephemeral, key, priority);
        }

        public Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Update<TEntity>(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Update<TEntity, TEntity>(storage, key, priority);
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
