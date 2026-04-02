using DataLayer.Utilities.Extensions;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DataLayer.Utilities
{
    public interface ILocalStore : IAsyncDisposable
    {
        ValueTask InitializeAsync();

        ValueTask<bool> SetupDatabaseAsync(string? dbName, Dictionary<string, Tuple<List<string>, List<KeyValuePair<string, List<string>>>>> schema);

        ValueTask<bool> NeedsInstall(string? dbName, List<KeyValuePair<string, List<string>>> columnNames);

        ValueTask PutRecordAsync<T>(string storeName, T record);

        ValueTask<T?> GetRecordAsync<T>(string storeName, object key);

        ValueTask<List<T>> QueryIndexAsync<T>(
            string storeName,
            string? indexName = null,
            object? exact = null,
            object? lower = null,
            object? upper = null);

        ValueTask<bool> DeleteRecordAsync(string storeName, object key);

        ValueTask<bool> DeleteOldDatabaseAsync(string? dbName = null);
        //public Task ModuleInitialize { get; }
        //public IJSObjectReference? Module { get; }
        bool NeedsInitialize { get; }

        bool IsReady { get; }
    }

    public interface IRenderStateProvider
    {
        bool IsRendered { get; }
        IJSRuntime Runtime { get; }
        event Action OnRendered;
        event Action OnEmptied;
        void NotifyEmptied();
        void NotifyRendered(IJSRuntime Runtime);
        Task WaitForRender { get; }
    }

    public class RenderStateProvider : IRenderStateProvider
    {

        private IJSRuntime? _runtime = null;
        public IJSRuntime Runtime { get
            {
                if (!_renderTcs.Task.IsCompleted || _runtime == null)
                {
                    throw new InvalidOperationException("JSRuntime is not available. Ensure that the component is rendered before registering for scroll events.");
                }
                return _runtime;
            }
            private set => _runtime = value;
        }

        // This is the task your LocalStore will 'Then' off of
        private Action? _onRendered;
        public event Action? OnRendered
        {
            add
            {
                _onRendered += value;
                // The "Sticky" logic: If the condition is already met, 
                // fire the callback for this specific subscriber immediately.
                if (IsRendered)
                {
                    value?.Invoke();
                }
            }
            remove => _onRendered -= value;
        }
        public event Action? OnEmptied;



        private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task WaitForRender => _renderTcs.Task;

        public bool IsRendered => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

        public void NotifyRendered(IJSRuntime runtime)
        {
            Runtime = runtime;
            // Fulfill the promise for everyone currently waiting
            _renderTcs.TrySetResult(true);
            _onRendered?.Invoke();
        }

        public void NotifyEmptied()
        {
            _runtime = null;
            // Only swap for a new "Promise" if the old one was already fulfilled.
            // If it's still pending, let the current waiters keep waiting for the NEXT render.
            if (_renderTcs.Task.IsCompleted)
            {
                _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            OnEmptied?.Invoke();
        }
    }




    public class LocalStore : ILocalStore
    {

        private readonly IRenderStateProvider Rendered;
        private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool NeedsInitialize { get; protected set; } = true;

        public bool IsReady => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

        public LocalStore(IRenderStateProvider _rendered)
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
            if (Module != null)
            {
                await Module.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }


        private Task ModuleInitialize { get => _renderTcs.Task; }
        

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
