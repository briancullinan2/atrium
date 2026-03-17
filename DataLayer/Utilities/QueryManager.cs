using DataLayer.Customization;
using DataLayer.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Net.Http.Json;

namespace DataLayer.Utilities
{

    public interface IQueryManager
    {
        Task<IEnumerable<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;
        Task<IEnumerable<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;
        Task<IEnumerable<TSet>> Synchronize<TSet>(bool FromPersistent, bool ToPersistent, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>;

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
        protected static IServiceProvider? _service { get; set; } = null;
        // Priority 0 = High (UI updates), 10 = Low (Background sync)
        protected virtual PriorityQueue<QueryTask, int> _taskQueue { get; } = new();
        protected virtual bool _isProcessing  { get; set; } = false;

        public QueryManager()
        {

        }

        public class QueryTask
        {
            public string? XmlQuery { get; set; }
            public Action<object>? OnSuccess { get; set; }
            public bool IsSaveOperation { get; set; }
        }


        public void Enqueue(string xml, Action<object> callback, int priority = 5)
        {
            _taskQueue.Enqueue(new QueryTask { XmlQuery = xml, OnSuccess = callback }, priority);
            _ = ProcessQueueAsync(); // Fire and forget worker
        }



        public virtual async Task ProcessQueueAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            while (_taskQueue.TryDequeue(out var task, out var priority))
            {
                try
                {
                    // Throttling: prevent bombarding the server
                    await Task.Delay(50);



                }
                catch (Exception)
                {
                    // TODO: log error, retry depending on type?
                }
            }

            _isProcessing = false;
        }



        public Task<IEnumerable<TSet>> Synchronize<TSet>(Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return Synchronize(true, false, qualifier, priority);
        }

        public Task<IEnumerable<TSet>> Synchronize<TSet>(StorageType From, StorageType To, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TSet>> Synchronize<TSet>(bool FromPersistent, bool ToPersistent, Expression<Func<TSet, bool>> qualifier, int priority = 10) where TSet : Entity<TSet>
        {
            return Synchronize(
                FromPersistent ? StorageType.Persistent : StorageType.Ephemeral,
                ToPersistent ? StorageType.Persistent : StorageType.Ephemeral,
                qualifier, priority);
        }


        public Task<TEntity> Save<TEntity>(Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Save(false, expression, priority);
        }

        public Task<TEntity> Save<TEntity>(TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Save(false, entity, priority);
        }



        public Task<TEntity> Save<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            throw new NotImplementedException();
        }

        public Task<TEntity> Save<TEntity>(StorageType storage, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            throw new NotImplementedException();
        }



        public Task<TEntity> Save<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> expression, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Save(persistent ? StorageType.Persistent : StorageType.Ephemeral, expression, priority);
        }

        public Task<TEntity> Save<TEntity>(bool persistent, TEntity entity, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Save(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }




        public Task<TResult> Query<TEntity, TResult>(Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Query(false, query, priority);
        }

        public Task<TResult> Query<TEntity, TResult>(StorageType storage, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            throw new NotImplementedException();
        }

        public Task<TResult> Query<TEntity, TResult>(bool persistent, Expression<Func<IQueryable<TEntity>, TResult>> query, int priority = 10) where TEntity : Entity<TEntity>
        {
            return Query(persistent ? StorageType.Persistent : StorageType.Ephemeral, query, priority);
        }




        public Task<TEntity> Update<TEntity>(Expression<Func<TEntity, TEntity>> key, int priority = 10)
        {
            return Update(false, key, priority);
        }

        public Task<object?> Update(object key, int priority = 10)
        {
            return Update(false, key, priority);
        }

        public Task<TEntity> Update<TEntity>(TEntity entity, int priority = 10)
        {
            return Update(false, entity, priority);
        }



        public Task<TEntity> Update<TEntity>(bool persistent, Expression<Func<TEntity, TEntity>> key, int priority = 10)
        {
            return Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public Task<object?> Update(bool persistent, object key, int priority = 10)
        {
            return Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, key, priority);
        }

        public Task<TEntity> Update<TEntity>(bool persistent, TEntity entity, int priority = 10)
        {
            return Update(persistent ? StorageType.Persistent : StorageType.Ephemeral, entity, priority);
        }



        public Task<TEntity> Update<TEntity>(StorageType storage, Expression<Func<TEntity, TEntity>> key, int priority = 10)
        {
            throw new NotImplementedException();
        }

        public Task<object?> Update(StorageType storage, object key, int priority = 10)
        {
            throw new NotImplementedException();
        }

        public Task<TEntity> Update<TEntity>(StorageType storage, TEntity entity, int priority = 10)
        {
            throw new NotImplementedException();
        }
    }


    public class RemoteManager : QueryManager
    {
        private readonly HttpClient? _httpClient;

        public RemoteManager()
        {
            _httpClient = _service?.GetRequiredService<HttpClient>();
        }

        public override async Task ProcessQueueAsync()
        {
            if(_httpClient == null)
            {
                throw new InvalidOperationException("No http client.");
            }


            if (_isProcessing) return;
            _isProcessing = true;

            while (_taskQueue.TryDequeue(out var task, out var priority))
            {
                try
                {
                    // Throttling: prevent bombarding the server
                    await Task.Delay(50);

                    var response = await _httpClient.PostAsJsonAsync("api/query", task.XmlQuery);
                    if (response.IsSuccessStatusCode)
                    {
                        var resultJson = await response.Content.ReadAsStringAsync();
                        // You'll need a way to know the Type here for deserialization
                        task.OnSuccess?.Invoke(resultJson);
                    }
                }
                catch (Exception) {
                    // TODO: log error, retry depending on type?

                }
            }

            _isProcessing = false;
        }

    }

}
