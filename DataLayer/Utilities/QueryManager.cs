using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace DataLayer.Utilities
{

    public interface IQueryManager
    {
        // TODO: for overriding in web client to switch persistent to remote, code reduction
        IQueryProvider? FinalProvider { get; set; }

        StorageType EphemeralStorage { get; set; }
        StorageType PersistentStorage { get; set; }
        Type EphemeralType { get; set; }
        Type PersistentType { get; set; }


        Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10)
            where TSet : Entity<TSet>;
        Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10)
            where TSet : Entity<TSet>;

        Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier, int priority = 10)
            where TSet : Entity<TSet>
            where TFrom : TranslationContext
            where TTo : TranslationContext;

        Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;
        Task<IEntity> Save(IEntity entity, int priority = 10);
        Task<IEntity> Save(StorageType storage, IEntity entity, int priority = 10);
        Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;



        //Task<IEntity> Save(bool persistent, IEntity entity, int priority = 10);


        Task<List<object>> Query(object query, string type, int priority = 10);
        AsyncQueryable<TEntity> Query<TEntity>(object query, int priority = 10) where TEntity : Entity<TEntity>;
        AsyncQueryable<TEntity> Query<TEntity>(Expression<Func<TEntity, bool>>? query = null, int priority = 10) where TEntity : Entity<TEntity>;
        //TResult Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        //TResult Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;


        Task<TResult> QueryNow<TEntity, TResult>(
            StorageType storage,
            Expression query,
            int priority = 10)
            where TEntity : class;


        Task<TEntity> Update<TEntity, TResult>(StorageType storage, Expression<Func<TEntity, TResult>> key, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Update<TEntity>(Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<IEntity?> Update(IEntity entity, int priority = 10);
        Task<IEntity?> Update(StorageType storage, IEntity entity, int priority = 10);

        Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;


        //Task ProcessQueueAsync();





        Expression? ToExpression(string query);
        Expression? ToExpression(StorageType? storage, string query);

        Expression? ToExpression(string query, out IQueryable? set);
        Expression? ToExpression(StorageType? storage, string query, out IQueryable? set);

        Task<object?> ToQueryable(string query);

        Task<object?> ToQueryable(string query, StorageType? storage);

        TranslationContext GetContext(StorageType type);

        TContext GetContext<TContext>(StorageType? type = null) where TContext : DbContext;

    }




    public class QueryManager(IServiceProvider Service) : IQueryManager
    {

        // Priority 0 = High (UI updates), 10 = Low (Background sync)
        protected static PriorityQueue<TaskCompletionSource, int> TaskQueue { get; } = new();
        private static readonly SemaphoreSlim _processorLock = new(1, 1);
        private static readonly SemaphoreSlim _gate = new(0);
        public IQueryProvider? FinalProvider { get; set; }

        public virtual StorageType EphemeralStorage { get; set; } = StorageType.Ephemeral;
        public virtual StorageType PersistentStorage { get; set; } = StorageType.Persistent;
        public virtual StorageType RemoteStorage { get; set; } = StorageType.Remote;
        public virtual StorageType TestStorage { get; set; } = StorageType.Test;
        
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
        






        public static async Task<object?> DetypeTask<TReturn>(Task<TReturn> Callback)
        {
            var result = await Callback;
            return result;
        }


        public async Task Enqueue(Func<Task> callback, int priority = 5)
        {
            await Enqueue(async () => { await callback(); return true; }, priority);
        }


        public async Task<TReturn> Enqueue<TReturn>(Func<Task<TReturn>> callback, int priority = 5)
        {
            var stackWhenCalled = new System.Diagnostics.StackTrace(true).ToString();

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

                //if (result is IQueryable queryable)
                //{
                // Use reflection or 'dynamic' to call ToList() 
                // This pulls the data into memory while the DbContext is still alive
                //    var list = await AsyncEnumerable.ToListAsync<TReturn>((dynamic)queryable);
                //    var finalResult = (TReturn)Queryable.AsQueryable(list);
                //    var finalType = typeof(TReturn);
                //    return (TReturn)finalResult;
                //}

                return result;
            }
            catch (Exception ex)
            {
                //if (TaskQueue.Count == 0)
                //    _processorLock.Release();
                Console.WriteLine("Managed query failed: " + callback.Method + "\n" + ex);
                Console.WriteLine("Stack when called: " + stackWhenCalled);
                throw new InvalidOperationException("holy shit", ex);
            }
            finally
            {
                // Tell the background loop we are done so it can release the next one
                _ = _gate.Release();
            }
        }


        public virtual async Task ProcessQueueAsync()
        {
            if (!await _processorLock.WaitAsync(0)) return;

            TaskCompletionSource? next = null;
            try
            {
                while (true)
                {
                    lock (TaskQueue)
                    {
                        if (!TaskQueue.TryDequeue(out next, out _)) break;
                    }

                    await Task.Yield();

                    // Use TrySetResult to signal the caller. 
                    // If it returns true, we MUST wait for the gate.
                    if (next.TrySetResult())
                    {
                        // In Flagstaff, 30s is a lifetime, but safe for a hang.
                        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(3)))
                        {
                            Console.WriteLine("Query gate timeout! Check for unhandled exceptions in Enqueue.");
                            // Force release if the caller disappeared without calling _gate.Release()
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }
            catch (Exception ex)
            {
                next?.TrySetException(ex);
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
                StorageType.Ephemeral => EphemeralStorage,
                StorageType.Persistent => PersistentStorage,
                StorageType.Remote => RemoteStorage,
                StorageType.Test => TestStorage,
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
            } switch
            {
                StorageType.Ephemeral => typeof(EphemeralStorage),
                StorageType.Persistent => typeof(PersistentStorage),
                StorageType.Remote => typeof(RemoteStorage),
                StorageType.Test => typeof(TestStorage),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
            };
        }




        public TContext GetContext<TContext>(StorageType? type = null) where TContext : DbContext
        {
            var contextType = type == null ? typeof(TContext) : GetStorageType(type ?? EphemeralStorage);
            return (TContext)Service.CreateScope().ServiceProvider.GetRequiredService(contextType);
        }


        public TranslationContext GetContext(StorageType type)
            => GetContext<TranslationContext>(type);



        public async Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return await Synchronize(PersistentStorage, EphemeralStorage, qualifier, priority);
        }

        public virtual async Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10)
            where TSet : Entity<TSet>
        {
            await Enqueue(async () =>
            {
                var contextFrom = GetContext(From);
                var contextTo = GetContext(To);
                if (contextFrom == null || contextTo == null)
                {
                    throw new InvalidOperationException("Database context failed in: " + nameof(Synchronize));
                }
                await contextFrom.InitializeIfNeeded();
                await contextTo.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () =>
            {
                var contextFrom = GetContext(From);
                var contextTo = GetContext(To);
                if (contextFrom == null || contextTo == null)
                {
                    throw new InvalidOperationException("Database context failed in: " + nameof(Synchronize));
                }
                return await SynchronizeNow(contextFrom, contextTo, qualifier);
            }, priority);
        }


        public virtual async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier, int priority = 10)
            where TSet : Entity<TSet>
            where TFrom : TranslationContext
            where TTo : TranslationContext
        {
            await Enqueue(async () =>
            {
                if (contextFrom == null || contextTo == null)
                {
                    throw new InvalidOperationException("Database context failed in: " + nameof(Synchronize));
                }
                await contextFrom.InitializeIfNeeded();
                await contextTo.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () =>
            {
                return await SynchronizeNow(contextFrom, contextTo, qualifier);
            }, priority);
        }


        protected virtual async Task<List<TSet>> SynchronizeNow<TFrom, TTo, TSet>(TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier)
            where TSet : Entity<TSet>
            where TFrom : TranslationContext
            where TTo : TranslationContext
        {
            using var transactionFrom = contextFrom.Database.BeginTransaction();
            using var transactionTo = contextTo.Database.BeginTransaction();

            var entities = await contextFrom.Set<TSet>().AsNoTracking().Where(qualifier).ToListAsync();

            // TODO: synchronize based on CanonicalFingerprint as the key
            // TODO: OR PrimaryKeyAttribute forces key canonization 
            // TODO: use internal save instead of automatic set management
            // TODO: save the main entity and use canonization on connected entities
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
            await transactionTo.CommitAsync();
            await transactionFrom.DisposeAsync();

            return await contextTo.Set<TSet>().Where(qualifier).ToListAsync();
        }



        public async Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(EphemeralStorage, expression, priority);
        }

        public async Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(EphemeralStorage, entity, priority);
        }

        protected async Task ShallowSaveRecursive<T>(DbContext persistentContext, T updatedEntity, bool recurse = true) where T : Entity<T>
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

            var UpdateGeneric = GetType().GetMethods(nameof(UpdateNow), 1, [typeof(DbContext)]).FirstOrDefault();

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


                var genericMethod = UpdateGeneric?.MakeGenericMethod(nav.TargetEntityType.ClrType)
                    ?? throw new InvalidOperationException("Could not render non-generic UpdateNow method.");

                var resolvedItems = new List<object>();
                foreach (var child in incomingList.Cast<object>().ToList() ?? Enumerable.Empty<object>())
                {
                    if (genericMethod.Invoke(this, [persistentContext, child, null, 3]) is Task task)
                    {
                        await task;
                        dynamic itemEntry = persistentContext.Entry((task as dynamic).Result);

                        // TODO: this worked for the recursion error so it's probably going to fuck me over in the near future.
                        if (itemEntry.Context != persistentContext || itemEntry.State != EntityState.Unchanged
                            && itemEntry.State != EntityState.Added)
                        {
                            ShallowSaveRecursive(persistentContext, (task as dynamic).Result, recurse);
                        }

                        itemEntry.CurrentValues.SetValues(child);

                        await persistentContext.SaveChangesAsync();

                        if (genericMethod.Invoke(this, [persistentContext, child, null, 3]) is Task task2)
                        {
                            await task2;

                            resolvedItems.Add((task2 as dynamic).Result);
                        }
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
            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () =>
            {
                var context = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await context.InitializeIfNeeded();
                using var transaction = context.Database.BeginTransaction();

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

                await transaction.DisposeAsync();

                return await SaveNow(storage, entity);
            }, priority);
        }


        public virtual async Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () =>
            {
                return await SaveNow(storage, entity);
            }, priority);
        }


        public virtual async Task<IEntity> Save(IEntity entity, int priority = 10)
        {
            return await Save(EphemeralStorage, entity, priority);
        }


        public virtual async Task<IEntity> Save(StorageType storage, IEntity entity, int priority = 10)
        {
            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () =>
            {
                return await SaveNow(storage, entity);
            }, priority);
        }


        protected virtual async Task<IEntity> SaveNow(StorageType storage, IEntity entity)
        {
            var RealSave = GetType().GetMethods(nameof(SaveNow), 1, [typeof(StorageType), typeof(object)])
                .FirstOrDefault()
                ?.MakeGenericMethod(entity.GetType())
                ?? throw new InvalidOperationException("Failed to render save now function.");
            var result = RealSave.Invoke(this, [storage, entity]);
            if (result is Task task)
            {
                await task;
                return (IEntity)(result as dynamic).Result;
            }
            throw new InvalidOperationException("Failed to render IEntity.");
        }


        protected virtual async Task<TEntity> SaveNow<TEntity>(StorageType storage, TEntity entity) where TEntity : Entity<TEntity>
        {
            var context = GetContext(storage)
                ?? throw new InvalidOperationException("Database context failed in: " + nameof(SaveNow));
            using var saveTransaction = context.Database.BeginTransaction();
            try
            {
                await ShallowSaveRecursive(context, entity);
                entity.CanonicalFingerprint = entity.GetHashCode();
                _ = await context.SaveChangesAsync();

                saveTransaction.Commit();

                return await UpdateNow(storage, entity);
            }
            catch (Exception ex)
            {
                saveTransaction.Rollback();
                throw new Exception("new bs", ex); // Rethrow so the parent catch can handle the fallback
            }
        }


        public virtual Task<List<object>> Query(object query, string type, int priority = 10)
        {
            var realType = type.ToType() ?? throw new InvalidOperationException("Entity type not known: " + type);
            var ToPredicate = typeof(DataLayer.Utilities.Extensions.ExpressionExtensions)
                .GetMethods("ToPredicate", 1, [typeof(object)])
                .FirstOrDefault()
                ?.MakeGenericMethod(realType)
                ?? throw new InvalidOperationException("Cant find ToPredicate");
            var Query = typeof(QueryManager)
                .GetMethods(nameof(QueryManager.Query), 1, [typeof(Expression)])
                .FirstOrDefault()
                ?.MakeGenericMethod(realType)
                ?? throw new InvalidOperationException("Cant find Query builder");
            var result = (IQueryable)Query.Invoke(this, [ToPredicate.Invoke(null, [query]), priority])!;
            return result.Cast<object>().ToListAsync();
        }


        public virtual AsyncQueryable<TEntity> Query<TEntity>(object query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Query<TEntity>(query.ToPredicate<TEntity>(), priority);
        }



        public AsyncQueryable<TEntity> Query<TEntity>(
            Expression<Func<TEntity, bool>>? query = null,
            int priority = 10) where TEntity : Entity<TEntity>
        {
            if (query == null)
            {
                // Return a simple identity: entities => entities
                return Query<TEntity, AsyncQueryable<TEntity>>(EphemeralStorage, entities => (AsyncQueryable<TEntity>)entities, priority);
            }

            // MANUALLY build the call to .Where(query) 
            // This prevents the compiler from wrapping 'query' in a DisplayClass lookup.
            var parameter = Expression.Parameter(typeof(IQueryable<TEntity>), "entities");

            // This creates: entities.Where(query)
            var whereCall = Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Where),
                [typeof(TEntity)],
                parameter,
                Expression.Quote(query) // Quote ensures the Lambda is treated as data/logic
            );

            // This creates the final Lambda: (IQueryable<TEntity> entities) => entities.Where(query)
            var lambda = Expression.Lambda<Func<IQueryable<TEntity>, IQueryable<TEntity>>>(whereCall, parameter);

            return (AsyncQueryable<TEntity>)Query<TEntity, IQueryable<TEntity>>(EphemeralStorage, lambda, priority);
        }



        public TResult Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Query<TEntity, TResult>(EphemeralStorage, query, priority);
        }





        public virtual TResult Query<TEntity, TResult>(
            StorageType storage,
            Expression<Func<IQueryable<TEntity>, TResult>> query,
            int priority = 10)
            where TEntity : Entity<TEntity>
        {
            var provider = new EnqueuedQueryProvider<TEntity>(this, storage, priority);

            if (typeof(IEnumerable).IsAssignableFrom(typeof(TResult)) && typeof(TResult) != typeof(string))
            {
                return (TResult)provider.CreateQuery<TEntity>(query);
            }
            else if (typeof(Task).IsAssignableFrom(typeof(TResult)))
            {
                return (dynamic)provider.ExecuteAsync<Task<TResult>>(query, new CancellationToken());
            }
            // Return the "fake" queryable that points to your Enqueue engine
            throw new InvalidOperationException("Don't know what you were expecting, buts not this.");
        }


        // TODO: make this only queue once per same result
        private static readonly ConcurrentDictionary<string, Task<object?>> _pendingQueries = new();

        public virtual async Task<TResult> QueryNow<TEntity, TResult>(
            StorageType storage,
            Expression query,
            int priority = 10)
            where TEntity : class
            //where TEntity : Entity<TEntity>
        {
            // 1. Generate a unique key for this specific query signature
            // For "absurd detail," you could use query.ToXDocument() if you've mapped it,
            // but ToString() catches 99% of identical LINQ structures.
            var variables = query.GetHashCode().ToString();
            try
            {
                variables = JsonSerializer.Serialize(query.ToDictionary()).ToSafe();
            }
            catch { }
            string queryKey = $"{typeof(TEntity).Name}_{typeof(TResult).Name}_{storage}_{query.ToString().ToSafe()}_{variables}";
            Console.WriteLine("Query Caching: " + queryKey);
            // 2. Check if this exact query is already "in flight"
            // GetOrAdd ensures that only one Task is created for the same key.

            // might already be in dictionary and won't be restarted after a crash
            if(_pendingQueries.ContainsKey(queryKey))
            {
                _ = ProcessQueueAsync();
            }


            var task = _pendingQueries.GetOrAdd(queryKey, _k =>
            {
                _ = Enqueue(async () =>
                {
                    var contextFrom = GetContext(storage)
                        ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                    await contextFrom.InitializeIfNeeded();
                }, 0);

                // This inner block only runs ONCE for the same queryKey
                return Enqueue(async () =>
                {
                    var context = GetContext(storage) ?? throw new InvalidOperationException("DB context failed in: " + nameof(Query));
                    using var transaction = context.Database.BeginTransaction();

                    try
                    {

                        IQueryable<TEntity> set = context.Set<TEntity>().AsQueryable();
                        if (typeof(EnqueuedQueryProvider<>).Extends(set.Provider.GetType()))
                        {
                            throw new InvalidOperationException("Must override internal query provider!");
                        }

                        var swappedRoot = new RootReplacementVisitor(set).Visit(query);
                        query = swappedRoot ?? throw new InvalidOperationException("Something went wrong swapping providers");

                        TResult? result = default;

                        if ((typeof(IQueryable).IsAssignableFrom(typeof(TResult)) && typeof(TResult) != typeof(string))
                            || (typeof(IQueryable).IsAssignableFrom(query.Type) && query.Type != typeof(string)))
                        {
                            // It's a sequence - force materialization to avoid SingleQueryingEnumerable leaks
                            var finalQueryable = (FinalProvider ?? set.Provider).CreateQuery(query);

                            // Force ToList to materialize it before the context is disposed
                            var typeParameter = finalQueryable.ElementType
                                ?? typeof(TResult).GetGenericArguments().FirstOrDefault()
                                ?? typeof(TResult).GetElementType()
                                ?? throw new InvalidOperationException("Couldn't extract generic type.");

                            var toListMethod = typeof(AsyncEnumerable)
                                .GetMethod(nameof(AsyncEnumerable.ToListAsync))
                                ?.MakeGenericMethod(typeParameter) ?? throw new InvalidOperationException("Couldn't render ToListAsync");

                            var listResult = toListMethod.Invoke(null, [finalQueryable, null]);

                            if (typeof(ValueTask<>).Extends(listResult?.GetType())
                                && (listResult as dynamic)?.AsTask() is Task forcedTask2)
                            {
                                await forcedTask2;
                                var forcedList = (forcedTask2 as dynamic).Result;
                                result = CollectionConverter.ConvertAsync(forcedList, typeof(TResult));
                            }
                            if (listResult is Task forcedTask)
                            {
                                await forcedTask;
                                var forcedList = (forcedTask as dynamic).Result;
                                result = CollectionConverter.ConvertAsync(forcedList, typeof(TResult));
                            }
                        }

                        else if ((FinalProvider ?? set.Provider) is IAsyncQueryProvider asyncProvider)
                        {

                            var taskType = typeof(Task<>).MakeGenericType(typeof(TResult));
                            var executeMethod = asyncProvider.GetType()
                                .GetMethods(nameof(IAsyncQueryProvider.ExecuteAsync))
                                .FirstOrDefault()
                                ?.MakeGenericMethod(taskType)
                                ?? throw new InvalidOperationException("Unable to render ExecuteAsync");
                            var unconverted = executeMethod.Invoke(asyncProvider,
                                [query, null]
                            // Always good practice to pass a token if available
                            //TODO: cancellationToken for UX to cancel log queries like searches
                            );
                            if (unconverted is Task task)
                            {
                                await task;
                                if (typeof(IEnumerable).IsAssignableFrom(typeof(TResult))
                                    && (unconverted as dynamic).Result != null)
                                {
                                    result = (TResult)CollectionConverter.ConvertAsync((unconverted as dynamic).Result, typeof(TResult))!;
                                }
                                else
                                {
                                    result = (unconverted as dynamic).Result;
                                }
                            }
                            else
                            {
                                result = (TResult)unconverted!;
                            }
                        }

                        else
                        {
                            // Fallback if the provider doesn't support async (e.g., Linq-to-Objects)
                            result = (TResult)(FinalProvider ?? set.Provider).Execute(query)!;
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
                        _pendingQueries.TryRemove(queryKey, out var _);
                        transaction.Dispose();
                    }
                }, priority);
            });

            // 4. All callers (original and late-comers) await the same task
            var finalResult = await task;
            var finalType = typeof(TResult);
            return (TResult)finalResult!;
        }


        protected async Task LoadAllNavigations<TEntity>(DbContext context, TEntity entity, Expression<Func<TEntity, bool>>? predicate = null, int depth = 5)
            where TEntity : Entity<TEntity>

        {
            if (depth <= 0) return;

            _ = predicate ?? entity.Predicate();

            var entry = context.Entry(entity);
            var navigations = entry.Metadata.GetNavigations()
                .Concat<INavigationBase>(entry.Metadata.GetSkipNavigations());

            var UpdateGeneric = GetType().GetMethods(nameof(UpdateNow), 1, [typeof(DbContext)]).FirstOrDefault();


            foreach (var navigation in navigations)
            {
                var navEntry = entry.Navigation(navigation.Name);
                if (!navEntry.IsLoaded)
                {
                    await navEntry.LoadAsync();
                }

                // 1. Get the current value (even if it's just a 'New' object with an ID)
                var navValue = navigation.GetGetter().GetClrValue(entity);

                if ((navigation.IsCollection || navigation is IReadOnlySkipNavigation) && navValue is IEnumerable collection)
                {
                    var resolvedItems = new List<object>();
                    var genericMethod = UpdateGeneric?.MakeGenericMethod(navigation.TargetEntityType.ClrType)
                        ?? throw new InvalidOperationException("Could not render non-generic UpdateNow method.");

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
                    var genericMethod = UpdateGeneric?.MakeGenericMethod(navigation.TargetEntityType.ClrType)
                        ?? throw new InvalidOperationException("Could not render non-generic UpdateNow method.");

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




        public async Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update<TEntity, TEntity>(EphemeralStorage, key, priority);
        }

        public async Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            return await Update(EphemeralStorage, entity, priority);
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

            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

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

            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () => { return await UpdateNow(storage, entity); }, priority);
        }

        public async Task<IEntity?> Update(IEntity entity, int priority = 10)
        {
            return await Update(EphemeralStorage, entity, priority);
        }

        public virtual async Task<IEntity?> Update(StorageType storage, IEntity entity, int priority = 10)
        {
            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

            var UpdateGeneric = GetType().GetMethods(nameof(UpdateNow), 1, [typeof(DbContext)]).FirstOrDefault();
            return await Enqueue(async () =>
            {
                var persistentContext = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                var genericMethod = UpdateGeneric?.MakeGenericMethod(entity.GetType());
                var result = genericMethod?.Invoke(this, [persistentContext, entity, null, 3]);
                if (result is Task task)
                {
                    await task;
                    return (result as dynamic).Result as IEntity;
                }
                return result as IEntity;
            }, priority);
        }


        public virtual async Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10)
            where TEntity : Entity<TEntity>
        {
            await Enqueue(async () =>
            {
                var contextFrom = GetContext(storage)
                    ?? throw new InvalidOperationException("Database context failed in: " + nameof(Save));
                await contextFrom.InitializeIfNeeded();
            }, 0);

            return await Enqueue(async () => { return await UpdateNow(storage, entity); }, priority);
        }

        protected virtual async Task<TEntity> UpdateNow<TEntity>(StorageType storage, TEntity entity)
            where TEntity : Entity<TEntity>

        {
            return await UpdateNow(storage, entity, entity.Predicate());
        }


        protected virtual async Task<TEntity> UpdateNow<TEntity>(StorageType storage, TEntity entity, Expression<Func<TEntity, bool>>? predicate = null)
            where TEntity : Entity<TEntity>

        {
            var context = GetContext(storage) ?? throw new InvalidOperationException("Database context failed in: " + nameof(UpdateNow));
            await context.InitializeIfNeeded();

            return await UpdateNow(context, entity, predicate, 3);
        }

        protected virtual async Task<TEntity> UpdateNow<TEntity>(DbContext context, TEntity entity, Expression<Func<TEntity, bool>>? predicate = null, int depth = 3)
            where TEntity : Entity<TEntity>

        {
            predicate ??= entity.Predicate();

            if (entity == null)
            {
                throw new InvalidOperationException("Entity is null.");
            }

            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var primaryKey = entityType?.FindPrimaryKey();
            var predicateValues = predicate.ToDictionary();

            // If the entity isn't being tracked, we need to attach it first
            if (context.Entry(entity).State == EntityState.Detached)
            {
                if (primaryKey?.Properties.Count == predicateValues.Values.Count(s => !string.IsNullOrWhiteSpace(s) && (!int.TryParse(s, out var key) || key != 0)))
                {
                    var existingEntity = context.Set<TEntity>().Local.AsQueryable().FirstOrDefault(predicate);
                    // TODO: make this an overload that accepts an IEnumberable as an output and iterates over all matches?
                    existingEntity ??= context.ChangeTracker.Entries<TEntity>().AsQueryable().Select(e => e.Entity).FirstOrDefault(predicate);
                    existingEntity ??= await context.Set<TEntity>().FirstOrDefaultAsync(predicate);

                    await LoadAllNavigations(context, entity, predicate, --depth);

                    if (existingEntity != null)
                    {
                        return existingEntity;
                    }
                }


                //context.Attach(entity);
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
            catch (Exception ex)
            {
                Console.WriteLine("Update entity failed. " + ex);
                throw new InvalidOperationException("Update entity failed. " + ex);
            }

            return entity;
        }

        public Task<TEntity> Update<TEntity>(Expression<Func<TEntity, bool>> key, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Update(EphemeralStorage, key, priority);
        }


        public Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Update<TEntity, TEntity>(storage, key, priority);
        }

        public Expression? ToExpression(string query, out IQueryable? set)
        {
            return ToExpression(EphemeralStorage, query, out set);
        }


        public Expression? ToExpression(string query)
        {
            return ToExpression(EphemeralStorage, query, out _);
        }

        public Expression? ToExpression(StorageType? storage, string query)
        {
            return ToExpression(EphemeralStorage, query, out _);
        }

        public Expression? ToExpression(StorageType? storage, string query, out IQueryable? set)
        {
            var context = GetContext(storage ?? EphemeralStorage)
                ?? throw new InvalidOperationException("Database context failed.");

            return ToExpression(query, context, out set);
        }

        protected static Expression? ToExpression(string query, TranslationContext context, out IQueryable? set)
        {
            LinqExtensions._parameters.Clear();
            using XmlReader reader = XmlReader.Create(new StringReader(query));
            _ = reader.MoveToContent();
            XElement root = (XElement)XNode.ReadFrom(reader);

            Expression? finalExpression = root.ToExpression(context, out set)
                ?? throw new InvalidOperationException("Could not convert expression document to Queryable: " + query);

            return finalExpression;
        }


        public async Task<object?> ToQueryable(string query)
        {
            return await ToQueryable(query, EphemeralStorage);
        }


        public async Task<object?> ToQueryable(string query, StorageType? storage)
        {
            Expression? finalExpression = ToExpression(storage, query, out IQueryable? set)
                ?? throw new InvalidOperationException("Could not convert expression document to Queryable: " + query);

            var QueryGeneric = GetType().GetMethods(nameof(QueryManager.QueryNow), 2, [typeof(StorageType), typeof(Expression)])
                .FirstOrDefault()
                 ?? throw new InvalidOperationException("Could not render QueryNow method");

            bool isCollection = typeof(IEnumerable).IsAssignableFrom(finalExpression.Type)
                    && finalExpression.Type != typeof(string);

            var innerType = finalExpression.Type.GetGenericArguments().FirstOrDefault();
            var entityType = set?.GetType().GetGenericArguments().FirstOrDefault()
                ?? throw new InvalidOperationException("Could not find entity type.");

            var QueryNow = QueryGeneric.MakeGenericMethod(entityType, finalExpression.Type);
            var result = QueryNow.Invoke(this, [storage, finalExpression, 10])
                ?? throw new InvalidOperationException("Could not render QueryNow function.");

            if (result is Task task)
            {
                await task;
                return (result as dynamic).Result;
            }
            return result;
        }

    }


}
