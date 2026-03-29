using DataLayer.Utilities.Extensions;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataLayer.Utilities
{
    public interface ILocalStore : IAsyncDisposable
    {
        ValueTask InitializeAsync();

        ValueTask<bool> SetupStoreAsync(string storeName, List<string> keyPath, List<KeyValuePair<string, List<string>>> columnNames);

        ValueTask PutRecordAsync<T>(string storeName, T record);

        ValueTask<T?> GetRecordAsync<T>(string storeName, object key);

        ValueTask<IEnumerable<T>> QueryIndexAsync<T>(
            string storeName,
            string indexName,
            object lower,
            object? upper = null,
            bool getAll = true);

        ValueTask<bool> DeleteRecordAsync(string storeName, object key);

        ValueTask<bool> DeleteOldDatabaseAsync(string? dbName = null);
        //public Task ModuleInitialize { get; }
        //public IJSObjectReference? Module { get; }
    }

    public class LocalStore : ILocalStore
    {
        private readonly IJSRuntime _js;

        public LocalStore(IJSRuntime JS)
        {
            _js = JS;
            // Start the import immediately
            ModuleInitialize = JS.InvokeAsync<IJSObjectReference>("import", "/_content/DataLayer/local.js")
                .AsTask().Then(mod => Module = mod);
        }

        private Task ModuleInitialize { get; }
        private IJSObjectReference? Module { get; set; }

        // This helper ensures the module is loaded before we try to use it
        private async ValueTask EnsureModuleLoaded()
        {
            if (Module == null)
            {
                await ModuleInitialize;
            }
        }

        public async ValueTask PutRecordAsync<T>(string storeName, T record)
        {
            await EnsureModuleLoaded();
            await Module!.InvokeVoidAsync("putRecord", storeName, record);
        }

        public async ValueTask<T?> GetRecordAsync<T>(string storeName, object key)
        {
            await EnsureModuleLoaded();
            return await Module!.InvokeAsync<T?>("getRecord", storeName, key);
        }

        public async ValueTask<IEnumerable<T>> QueryIndexAsync<T>(string storeName, string indexName, object lower, object? upper = null, bool getAll = true)
        {
            await EnsureModuleLoaded();
            return await Module!.InvokeAsync<IEnumerable<T>>("queryIndex", storeName, indexName, lower, upper, getAll);
        }

        public async ValueTask<bool> SetupStoreAsync(string storeName, List<string> keyPath, List<KeyValuePair<string, List<string>>> columnNames)
        {
            await EnsureModuleLoaded();
            return await Module!.InvokeAsync<bool>("setupStore", storeName, keyPath, columnNames);
        }

        public async ValueTask<bool> DeleteRecordAsync(string storeName, object key)
        {
            await EnsureModuleLoaded();
            return await Module!.InvokeAsync<bool>("deleteRecord", storeName, key);
        }

        public async ValueTask<bool> DeleteOldDatabaseAsync(string? dbName = null)
        {
            await EnsureModuleLoaded();
            return await Module!.InvokeAsync<bool>("deleteOldDatabase", dbName);
        }

        public async ValueTask InitializeAsync() => await EnsureModuleLoaded();

        public async ValueTask DisposeAsync()
        {
            if (Module != null)
            {
                await Module.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }
    }

}
