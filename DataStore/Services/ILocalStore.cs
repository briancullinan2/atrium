using Microsoft.JSInterop;


namespace DataStore.Services
{
    

    public class LocalStore : ILocalStore
    {

        private readonly IRenderState Rendered;
        private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool NeedsInitialize { get; protected set; } = true;

        public bool IsReady => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

        public LocalStore(IRenderState _rendered)
        {
            Rendered = _rendered;
            // Start the import immediately
            Rendered.OnRendered += NotifyRendered;
            Rendered.OnEmptied += NotifyEmptied;
        }

        protected void NotifyEmptied() {
            if (_renderTcs.Task.IsCompleted)
                _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        protected void NotifyRendered() => _ = EnsureModuleLoaded();

        public async ValueTask DisposeAsync()
        {
            Rendered.OnRendered -= NotifyRendered;
            Rendered.OnEmptied -= NotifyEmptied;
            if (IsReady)
            {
                await Module.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }


        private Task ModuleInitialize => _renderTcs.Task;


        private IJSObjectReference? _module = null;
        public IJSObjectReference Module
        {
            get
            {
                if (!_renderTcs.Task.IsCompleted || _module == null)
                {
                    throw new InvalidOperationException("Module is not available. Must await ModuleInitialize before refering to JS module.");
                }
                return _module;
            }
            private set => _module = value;
        }


        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private async Task EnsureModuleLoaded()
        {
            // 1. Quick check outside the lock for performance
            if (_renderTcs.Task.IsCompleted) return;

            // 2. Wait for the lock
            await _loadLock.WaitAsync();

            try
            {
                // 3. Re-check inside the lock (The "Double-Check" pattern)
                if (_renderTcs.Task.IsCompleted) return;
                _module = await Rendered.Runtime.InvokeAsync<IJSObjectReference>("import", "/_content/DataLayer/local.js");
                _renderTcs.TrySetResult(true);
            }
            finally
            {
                // 4. Always release the lock in a finally block
                _loadLock.Release();
            }
        }

        public async ValueTask PutRecordAsync<T>(string storeName, T record)
        {
            await ModuleInitialize;
            await Module!.InvokeVoidAsync("putRecord", storeName, record);
        }

        public async ValueTask<T?> GetRecordAsync<T>(string storeName, object key)
        {
            await ModuleInitialize;
            return await Module!.InvokeAsync<T?>("getRecord", storeName, key);
        }

        public async ValueTask<List<T>> QueryIndexAsync<T>(string storeName, string? indexName = null, object? exact = null, object? lower = null, object? upper = null)
        {
            await ModuleInitialize;
            var result = await Module!.InvokeAsync<List<T>>("queryIndex", storeName, indexName, exact, lower, upper);
            return result ?? [];
        }

        public async ValueTask<bool> SetupDatabaseAsync(string? dbName, Dictionary<string, Tuple<List<string>, List<KeyValuePair<string, List<string>>>>> schema)
        {
            await ModuleInitialize;
            var result = await Module!.InvokeAsync<Tuple<bool?, string?>>("setupDatabase", dbName, schema.ToList());
            if (result.Item1 != true)
            {
                throw new InvalidOperationException("Failed to create store: " + result.Item2 + " for " + JsonSerializer.Serialize(schema));
            }
            NeedsInitialize = false;
            return true;
        }

        public async ValueTask<bool> DeleteRecordAsync(string storeName, object key)
        {
            await ModuleInitialize;
            return await Module!.InvokeAsync<bool>("deleteRecord", storeName, key);
        }

        public async ValueTask<bool> DeleteOldDatabaseAsync(string? dbName = null)
        {
            await ModuleInitialize;
            var result = await Module!.InvokeAsync<bool>("deleteOldDatabase", dbName);
            if (result == false)
                throw new InvalidOperationException("delete database failed.");
            return true;
        }

        public async ValueTask InitializeAsync() => await ModuleInitialize;


        public async ValueTask<bool> NeedsInstall(string? dbName, List<KeyValuePair<string, List<string>>> columnNames)
        {
            await ModuleInitialize;

            var metadata = await Module!.InvokeAsync<List<KeyValuePair<string?, object?>>>("getDatabaseMetadata", dbName, columnNames);
            if (metadata.Count == 0) return true; // don't even have a database

            var result = false;

            foreach (var db in metadata)
            {
                var needsInstall = await Module!.InvokeAsync<Tuple<string?, object?, bool?, List<string?>?>>("needsInstall", db.Key, columnNames);
                if (needsInstall.Item3 == true || needsInstall.Item4?.Count > 0)
                {
                    try
                    {
                        await DeleteOldDatabaseAsync(needsInstall.Item1);
                    } catch { }
                    result = true;
                }
            }

            NeedsInitialize = result;
            return result;
        }
    }

}
