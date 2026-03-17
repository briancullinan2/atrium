using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Net.Http.Json;

namespace DataLayer.Utilities
{

    public interface IQueryManager
    {
        Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;
        Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;
        Task<List<TSet>> Synchronize<TSet>(bool FromPersistent, bool ToPersistent, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;

        Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Save<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TEntity> Save<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TResult> Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;
        Task<TResult> Query<TEntity, TResult>(bool persistent, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>;

        Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10);
        Task<object?> Update(object key, int priority = 10);
        Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10);

        Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> key, int priority = 10);
        Task<object?> Update(bool persistent, object key, int priority = 10);
        Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10);

        Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10);
        Task<object?> Update(StorageType storage, object key, int priority = 10);
        Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10);

        Task ProcessQueueAsync();
    }


    public class QueryManager : IQueryManager
    {
        protected static IServiceProvider? Service { get; set; } = null;
        // Priority 0 = High (UI updates), 10 = Low (Background sync)
        protected virtual PriorityQueue<TaskCompletionSource, int> TaskQueue { get; } = new();
        protected virtual bool IsProcessing { get; set; } = false;
        private readonly SemaphoreSlim _gate = new(0);



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
                return await callback();
            }
            finally
            {
                // Tell the background loop we are done so it can release the next one
                _gate.Release();
            }
        }

        public virtual async Task ProcessQueueAsync()
        {
            lock (this)
            {
                if (IsProcessing) return;
                IsProcessing = true;
            }

            while (true)
            {
                TaskCompletionSource? next;
                lock (TaskQueue)
                {
                    if (!TaskQueue.TryDequeue(out next, out _)) break;
                }

                // Throttling
                await Task.Delay(50);

                // Trigger the caller's 'await myTurn.Task'
                next.SetResult();

                // Wait for the caller to finish their work (via the release in 'finally')
                await _gate.WaitAsync();
            }

            lock (this) { IsProcessing = false; }
        }




        public static Type GetStorageType(StorageType type) => type switch
        {
            StorageType.Ephemeral => typeof(EphemeralStorage),
            StorageType.Persistent => typeof(PersistentStorage),
            StorageType.Remote => typeof(RemoteStorage),
            StorageType.Test => typeof(TestStorage),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
        };


        public static Type GetContextType(StorageType type) => type switch
        {
            StorageType.Ephemeral => typeof(IDbContextFactory<EphemeralStorage>),
            StorageType.Persistent => typeof(IDbContextFactory<PersistentStorage>),
            StorageType.Remote => typeof(IDbContextFactory<RemoteStorage>),
            StorageType.Test => typeof(IDbContextFactory<TestStorage>),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Type {type} not mapped.")
        };


        public static IDbContextFactory<TContext>? GetContextFactory<TContext>() where TContext : DbContext => 
            Service?.GetService<IDbContextFactory<TContext>>();

        public static IDbContextFactory<TContext>? GetContextFactory<TContext>(Type contextType) where TContext : DbContext =>
            Service?.GetService(contextType) as IDbContextFactory<TContext>;


        public static TContext? GetContext<TContext>() where TContext : DbContext =>
            GetContextFactory<TContext>()?.CreateDbContext();

        public static TContext? GetContext<TContext>(Type contextType) where TContext : DbContext =>
            GetContextFactory<TContext>(contextType)?.CreateDbContext();

        public static TranslationContext? GetContext(Type contextType) =>
            typeof(QueryManager).GetMethod(nameof(GetContext), 1, [typeof(Type)])?.Invoke(null, [contextType]) as TranslationContext;



        public async Task<List<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return await Synchronize(true, false, qualifier, priority);
        }

        public async Task<List<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return await Enqueue(async () =>
            {
                using var scope = Service?.CreateScope();
                var contextFromType = GetContextType(From);
                var contextToType = GetContextType(To);
                var contextFrom = GetContext(contextFromType);
                var contextTo = GetContext(contextToType);
                if (contextFrom == null || contextTo == null)
                {
                    throw new InvalidOperationException("Database context failed.");
                }
                await contextFrom.Sync(contextTo, qualifier);
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



        public async Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            throw new NotImplementedException();
        }

        public async Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            throw new NotImplementedException();
        }



        public async Task<TEntity> Save<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(persistent ? StorageType.Persistent : StorageType.Ephemeral, expression, priority);
        }

        public async Task<TEntity> Save<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Save(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }




        public async Task<TResult> Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Query(false, query, priority);
        }

        public async Task<TResult> Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            throw new NotImplementedException();
        }

        public async Task<TResult> Query<TEntity, TResult>(bool persistent, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return await Query(persistent ? StorageType.Persistent : StorageType.Ephemeral, query, priority);
        }




        public async Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10)
        {
            return await Update(false, key, priority);
        }

        public async Task<object?> Update(object key, int priority = 10)
        {
            return await Update(false, key, priority);
        }

        public async Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10)
        {
            return await Update(false, entity, priority);
        }



        public async Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> key, int priority = 10)
        {
            return await Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public async Task<object?> Update(bool persistent, object key, int priority = 10)
        {
            return Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public async Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10)
        {
            return await Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }



        public async Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10)
        {
            throw new NotImplementedException();
        }

        public async Task<object?> Update(StorageType storage, object key, int priority = 10)
        {
            throw new NotImplementedException();
        }

        public async Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10)
        {
            throw new NotImplementedException();
        }
    }


    public class RemoteManager : QueryManager
    {
        private readonly HttpClient? _httpClient;

        public RemoteManager()
        {
            _httpClient = Service?.GetRequiredService<HttpClient>();
        }



    }

}
