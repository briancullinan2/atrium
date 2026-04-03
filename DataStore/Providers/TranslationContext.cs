using DataLayer.Utilities.Extensions;
using DataStore.Entities;
using DataStore.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace DataStore.Providers
{

    public abstract class TranslationContext<TEntity>(DbContextOptions ctx) : DbContext(ctx), ITranslationContext
    {

        static TranslationContext()
        {
            var assemblies = typeof(TEntity).Assembly.GetAssemblies(Assembly.GetCallingAssembly());
            _cachedTypes = assemblies.Distinct().SelectMany(a => a.GetTypes()).Distinct().ToList();
        }
        private static readonly List<Type> _cachedTypes;

        private static List<Type>? CachedEntities { get; set; }
        public static List<Type> EntityTypes => CachedEntities
            ??= [.. _cachedTypes.Where(t => t.IsClass && !t.IsAbstract && t.Extends(typeof(TEntity)) && t.IsConcrete() && t != typeof(object))];

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.Ignore<System.Text.RegularExpressions.Capture>();
            _ = modelBuilder.Ignore<System.Text.RegularExpressions.Match>();
            _ = modelBuilder.Ignore<System.Text.RegularExpressions.Group>();

            foreach (var type in EntityTypes)
            {
                modelBuilder.Entity(type).ToTable(type.Table());

            }

        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {

            foreach (var type in EntityTypes ?? [])
            {
                var props = type.Database().Where(p => p.PropertyType.Extends(typeof(Enum)));

                foreach (var prop in props)
                {
                    _ = configurationBuilder.Properties(prop.PropertyType).HaveConversion<int>();

                }
            }
        }
    }




    // This context never connects to a DB; it just holds your Entity mappings
    public class TranslationContext(IQueryManager query, DbContextOptions ctx) : TranslationContext<IEntity>(ctx)
    {
        public IQueryManager Query { get; set; } = query;


        public string ConnectString
        {
            get
            {
                if (!Database.IsRelational()) return "RemoteShell";
                return Database.GetDbConnection().ConnectionString;
            }
        }

        public bool NeedsInitialize { get; protected set; } = true;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);
            options.AddInterceptors(WrapperInterceptor.Instance);
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            NeedsInitialize = true;
        }

        private Task? _initializeTask;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public virtual Task InitializeIfNeeded()
        {
            // 1. Fast path: if already done, return completed task
            if (!NeedsInitialize && _initializeTask?.IsCompletedSuccessfully == true)
            {
                return Task.CompletedTask;
            }

            // 2. Lock to ensure only one thread creates the task
            lock (_initLock)
            {
                if (_initializeTask == null || _initializeTask.IsFaulted)
                {
                    _initializeTask = PerformInitialization();
                }
                return _initializeTask;
            }
        }

        protected virtual async Task PerformInitialization()
        {
            // Use the Semaphore to ensure even with the lock above, 
            // the actual async work is serialized.
            await _initLock.WaitAsync();
            NeedsInitialize = false;
            try
            {
                var conn = Database.GetDbConnection();
                //await conn.CloseAsync(); // This clears the internal transaction state

                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                // Re-check inside the lock
                using var transaction = Database.BeginTransaction();
                if (!NeedsInitialize) return;

                await Database.EnsureCreatedAsync();
                await EnsureGlobalIdentityStart();
                await SaveChangesAsync();

                await transaction.CommitAsync();
                NeedsInitialize = false;

            }
            catch (Exception ex)
            {
                // Reset the task so a retry can occur later
                _initializeTask = null;
                throw new InvalidOperationException("Database creation failed.", ex);
            }
            finally
            {
                _initLock.Release();
            }
        }


        // this is so we can use 0 id as new records
        public virtual async Task EnsureGlobalIdentityStart()
        {
            Console.WriteLine("Inserting 0 IDs.");

            // 1. Get all entities defined in your DbContext
            var entityTypes = Model.GetEntityTypes();

            foreach (var entityType in entityTypes)
            {
                // 2. Find the Primary Key
                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey == null) continue;

                // 3. Check if any part of the PK is Identity/AutoIncrement
                var hasIdentity = primaryKey.Properties.Any(p =>
                    p.ValueGenerated == ValueGenerated.OnAdd);

                if (hasIdentity)
                {
                    // 4. Get the actual Table Name (handling schema if necessary)
                    var tableName = entityType.GetTableName();
                    if (string.IsNullOrEmpty(tableName)) continue;

                    // 5. Update the SQLite sequence table
                    // We use INSERT OR IGNORE first to ensure the row exists, 
                    // then we don't accidentally overwrite a high sequence with 0 
                    // if there is already data (Safety First).
                    Database.ExecuteSql(
                        $"INSERT OR IGNORE INTO sqlite_sequence (name, seq) VALUES ('{tableName}', 0);");
                }
            }
        }
    }

    public class WrapperInterceptor : IMaterializationInterceptor
    {
        public static readonly WrapperInterceptor Instance = new();
        private WrapperInterceptor() { }
        public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
        {
            if (instance is IEntity entity && materializationData.Context is TranslationContext transCtx)
            {
                // Pull the Query manager directly from the context instance 
                // that is currently doing the materializing.
                entity.QueryManager = transCtx.Query;
                entity.ContextType = transCtx.GetType();
                return entity;
            }
            return instance;
        }
    }



    // expected to reset only the first time the application runs and be persistent on disk
    public class PersistentStorage(IQueryManager service, DbContextOptions<PersistentStorage> ctx) : TranslationContext(service, ctx)
    {
    }


    // expected to reset once at the beginning of application load
    public class EphemeralStorage(IQueryManager service, DbContextOptions<EphemeralStorage> ctx) : TranslationContext(service, ctx)
    {
        private static KeepAlive? _keepAliveConnection;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);
            if (_keepAliveConnection == null)
            {
                _keepAliveConnection = new KeepAlive("Data Source=:memory:");
                _keepAliveConnection.Open(); // The DB is born
            }
            options.UseSqlite(_keepAliveConnection);
        }
    }


    // default interface between web client and http host server
    public class RemoteStorage(HttpClient client, IQueryManager service, DbContextOptions<RemoteStorage> ctx) : TranslationContext(service, ctx)
    {
        public HttpClient Client { get; set; } = client;
        public string? BaseAddress { get; set; } = client.BaseAddress?.ToString();


        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            _ = options.UseInMemoryDatabase("RemoteShell");
            _ = options.ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            _ = options.ReplaceService<IQueryProvider, RemoteQueryProvider>();
            _ = options.ReplaceService<IAsyncQueryProvider, RemoteQueryProvider>();
        }


        protected override async Task PerformInitialization()
        {
            if (NeedsInitialize)
            {
                NeedsInitialize = false;
                //await Database.EnsureCreatedAsync();
            }
        }
        public override async Task EnsureGlobalIdentityStart()
        {
        }
    }


    // expected to reset multiple times per instance run
    public class TestStorage(ILocalStore _store, IQueryManager service, DbContextOptions<TestStorage> ctx) : TranslationContext(service, ctx)
    {
        public ILocalStore Store { get; } = _store;


        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            _ = options.UseInMemoryDatabase("TestShell");
            _ = options.ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            _ = options.ReplaceService<IQueryProvider, LocalQueryProvider>();
            _ = options.ReplaceService<IAsyncQueryProvider, LocalQueryProvider>();

        }

        public override async Task EnsureGlobalIdentityStart()
        {
            // TODO: save IDs in the settings database

        }



        private readonly SemaphoreSlim _initLock = new(1, 1);

        protected override async Task PerformInitialization()
        {
            await _initLock.WaitAsync();
            if (!NeedsInitialize)
            {
                return;
            }

            try
            {
                NeedsInitialize = false;

                if (!Store.NeedsInitialize) return;

                // TODO: check actual store for initialization needs
                var tables = IEntityExtensions.Schemas(this).Select(kvp => kvp.Name);
                var schema = IEntityExtensions.Schemas(this).ToDictionary(kvp => kvp.Name, kvp =>
                {
                    var predicate = IEntityExtensions.Predicate(kvp.EntityType)
                        .Select(p => p.Name)
                        .ToList();
                    var columns = IEntityExtensions.Database(kvp.EntityType, true /* list all database properties including keys */ )
                        .ToDictionary<PropertyInfo, string, List<string>>(p => p.Name, p => [p.Name]);
                    var indexes = IEntityExtensions.Indexes(kvp.EntityType, true /* include primary key as a natural index */ )
                        .ToDictionary<KeyValuePair<string, List<PropertyInfo>>, string, List<string>>(p =>
                            string.Join("", p.Value.Select(p => p.Name)) /* p.Key */, p => [.. p.Value.Select(p => p.Name)]);
                    var distinct = columns.Concat(indexes).DistinctBy(k => k.Key).ToList();
                    return new Tuple<List<string>, List<string>, List<KeyValuePair<string, List<string>>>>(predicate, [.. columns.Select(kvp => kvp.Key)], distinct);
                });

                // check schema integrity because IDB lets us do this
                var needInstall = await Store.NeedsInstall(null, [.. schema.Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.Item2))]);

                if (!needInstall)
                {
                    Console.WriteLine("Skipping install");
                    return;
                }

                try
                {
                    Console.WriteLine("Creating store");

                    var serializedNames = schema.ToDictionary(kvp =>
                        kvp.Key, kvp => new Tuple<List<string>, List<KeyValuePair<string, List<string>>>>(
                            [.. kvp.Value.Item1.Select(pathKey => pathKey.ToCamelCase())],
                            [..kvp.Value.Item3.Select(indexNameAndKeys => KeyValuePair.Create<string, List<string>>(
                                // additional index names don't matter but here I am fixing it because I was confused
                                //   why they didn't match when looking at it in the browser
                                indexNameAndKeys.Key.ToCamelCase() /*name must match RemoteManager.QueryNow*/,
                                [..indexNameAndKeys.Value.Select(p => p.ToCamelCase())]))]));

                    await Store.SetupDatabaseAsync(null, serializedNames);
                    await EnsureGlobalIdentityStart();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            catch (Exception ex)
            {
                // Reset the task so a retry can occur later
                Console.WriteLine(ex);
                throw new InvalidOperationException("Database creation failed.", ex);
            }
            finally
            {
                _initLock.Release();
            }


        }
    }

    public class KeepAlive(string conn) : SqliteConnection(conn)
    {
        public override void Close()
        {
            //base.Close();
        }

        public override async Task CloseAsync()
        {
            // return base.CloseAsync();
        }

        protected override void Dispose(bool disposing)
        {

        }
    }


}
