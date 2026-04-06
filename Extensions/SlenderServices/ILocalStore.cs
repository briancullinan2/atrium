using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices
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


}
