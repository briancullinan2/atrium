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
            string indexName,
            object? exact = null,
            object? lower = null,
            object? upper = null);

        ValueTask<bool> DeleteRecordAsync(string storeName, object key);

        ValueTask<bool> DeleteOldDatabaseAsync(string? dbName = null);
        //public Task ModuleInitialize { get; }
        //public IJSObjectReference? Module { get; }
        bool NeedsInitialize { get; }

    }

    public interface IRenderStateProvider
    {
        bool IsRendered { get; }
        IJSRuntime? Runtime { get; }
        event Action OnRendered;
        event Action OnEmptied;
        void NotifyEmptied(IJSRuntime Runtime);
        void NotifyRendered(IJSRuntime Runtime);
    }

    public class RenderStateProvider : IRenderStateProvider
    {

        public IJSObjectReference? Module { get; private set; }
        public IJSRuntime? Runtime { get; private set; }

        public bool IsRendered { get => _renderTcs.Task.IsCompleted; private set => _renderTcs.TrySetResult(value); }

        private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // This is the task your LocalStore will 'Then' off of
        public event Action? OnRendered;
        public event Action? OnEmptied;

        public void NotifyEmptied(IJSRuntime _runtime)
        {
            IsRendered = false;
            _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            OnEmptied?.Invoke();
        }

        // This is called by your MainLayout or Root component
        public void NotifyRendered(IJSRuntime _runtime)
        {
            IsRendered = true;
            Runtime = _runtime;
            OnRendered?.Invoke();
        }
    }


    public class LocalStore : ILocalStore
    {
        private readonly IRenderStateProvider _service;
        private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool NeedsInitialize { get; protected set; } = true;


        public LocalStore(IRenderStateProvider service)
        {
            _service = service;
            // Start the import immediately
            service.OnRendered += () => _ = EnsureModuleLoaded();
            service.OnEmptied += () =>
            {
                _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            };
            if (service.IsRendered)
            {
                _ = EnsureModuleLoaded();
            }
        }



        private Task ModuleInitialize { get => _renderTcs.Task; }
        private IJSObjectReference? Module { get; set; }

        // This helper ensures the module is loaded before we try to use it
        private async Task EnsureModuleLoaded()
        {
            if (_renderTcs.Task.IsCompleted) return;
            var result = _service.Runtime?.InvokeAsync<IJSObjectReference>("import", "/_content/DataLayer/local.js").AsTask();
            if (result is Task task)
            {
                await task;
                Module = (result as dynamic).Result;
            }

            _renderTcs.TrySetResult(true);
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

        public async ValueTask<List<T>> QueryIndexAsync<T>(string storeName, string indexName, object? exact = null, object? lower = null, object? upper = null)
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

        public async ValueTask DisposeAsync()
        {
            if (Module != null)
            {
                await Module.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }

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
                    await DeleteOldDatabaseAsync(needsInstall.Item1);
                    result = true;
                }
            }

            return result;
        }
    }

}
