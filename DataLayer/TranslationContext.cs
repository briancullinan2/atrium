using DataLayer.Entities;
using DataLayer.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataLayer
{
    // This context never connects to a DB; it just holds your Entity mappings
    public class TranslationContext(IQueryManager query, DbContextOptions ctx) : DbContext(ctx)
    {
        public IQueryManager Query { get; set; } = query;
        public DbSet<Permission>? Permissions { get; set; }
        public DbSet<Role>? Roles { get; set; }
        public DbSet<User>? Users { get; set; }
        public DbSet<Setting>? Settings { get; set; }
        public DbSet<Message>? Messages { get; set; }
        public DbSet<Pack>? Packs { get; set; }
        public DbSet<Card>? Cards { get; set; }
        public DbSet<Answer>? Answers { get; set; }
        public DbSet<Group>? Groups { get; set; }
        public DbSet<Entities.File>? Files { get; set; }
        public DbSet<Visit>? Visits { get; set; }
        public DbSet<Session>? Sessions { get; set; }
        public DbSet<Schedule>? Schedules { get; set; }
        public DbSet<Subject>? Subjects { get; set; }
        public DbSet<Grade>? Grades { get; set; }
        public DbSet<Lesson>? Lessons { get; set; }

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

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            _ = configurationBuilder.Properties<DisplayType>().HaveConversion<int>();
            _ = configurationBuilder.Properties<ControlMode>().HaveConversion<int>();
            _ = configurationBuilder.Properties<Gender>().HaveConversion<int>();
            _ = configurationBuilder.Properties<PackMode>().HaveConversion<int>();
            _ = configurationBuilder.Properties<PackStatus>().HaveConversion<int>();
            _ = configurationBuilder.Properties<CardType>().HaveConversion<int>();
            _ = configurationBuilder.Properties<GradeScale>().HaveConversion<int>();
            _ = configurationBuilder.Properties<DefaultPermissions>().HaveConversion<string>();
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.Ignore<System.Text.RegularExpressions.Capture>();
            _ = modelBuilder.Ignore<System.Text.RegularExpressions.Match>();
            _ = modelBuilder.Ignore<System.Text.RegularExpressions.Group>();


            _ = modelBuilder.Entity<Message>().ToTable(EntityMetadata.Message.TableName);
            _ = modelBuilder.Entity<Permission>().ToTable(EntityMetadata.Permission.TableName);
            _ = modelBuilder.Entity<Role>().ToTable(EntityMetadata.Role.TableName);
            _ = modelBuilder.Entity<Setting>().ToTable(EntityMetadata.Setting.TableName);
            _ = modelBuilder.Entity<User>().ToTable(EntityMetadata.User.TableName);
            _ = modelBuilder.Entity<Card>().ToTable(EntityMetadata.Card.TableName);
            _ = modelBuilder.Entity<Pack>().ToTable(EntityMetadata.Pack.TableName);
            _ = modelBuilder.Entity<Answer>().ToTable(EntityMetadata.Answer.TableName);
            _ = modelBuilder.Entity<Group>().ToTable(EntityMetadata.Group.TableName);
            _ = modelBuilder.Entity<Entities.File>().ToTable(EntityMetadata.File.TableName);
            _ = modelBuilder.Entity<Visit>().ToTable(EntityMetadata.Visit.TableName);
            _ = modelBuilder.Entity<Session>().ToTable(EntityMetadata.Session.TableName);
            _ = modelBuilder.Entity<Subject>().ToTable(EntityMetadata.Subject.TableName);
            _ = modelBuilder.Entity<Schedule>().ToTable(EntityMetadata.Schedule.TableName);
            _ = modelBuilder.Entity<Grade>().ToTable(EntityMetadata.Grade.TableName);
            _ = modelBuilder.Entity<Lesson>().ToTable(EntityMetadata.Lesson.TableName);

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
            try
            {
                // Re-check inside the lock
                NeedsInitialize = false;
                using var transaction = Database.BeginTransaction();
                if (!NeedsInitialize) return;

                var conn = Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

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


    public partial class EntityMetadata
    {
        public static EntityMetadata<Answer> Answer => new();
        public static EntityMetadata<Pack> Pack => new();
        public static EntityMetadata<Card> Card => new();
        public static EntityMetadata<Permission> Permission => new();
        public static EntityMetadata<User> User => new();
        public static EntityMetadata<Role> Role => new();
        public static EntityMetadata<Setting> Setting => new();
        public static EntityMetadata<Message> Message => new();
        public static EntityMetadata<Entities.File> File => new();
        public static EntityMetadata<Group> Group => new();
        public static EntityMetadata<Visit> Visit => new();
        public static EntityMetadata<Session> Session => new();
        public static EntityMetadata<Subject> Subject => new();
        public static EntityMetadata<Schedule> Schedule => new();
        public static EntityMetadata<Grade> Grade => new();
        public static EntityMetadata<Lesson> Lesson => new();

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
            //#pragma warning disable EF1001 // Internal EF Core API usage.
            //            _ = options.ReplaceService<IQueryCompiler, RemoteQuery>();
            //#pragma warning restore EF1001 // Internal EF Core API usage.
            /*options.ReplaceService<IQueryCompiler, Utilities.RemoteQuery>(
            (IServiceProvider internalServiceProvider, IQueryCompiler originalCompiler) =>
            {
                var currentContext = internalServiceProvider.GetRequiredService<ICurrentDbContext>();

                return new Utilities.RemoteQuery(
                    originalCompiler,
                    currentContext,
                    remoteUrl
                );
            });
            */
        }


        protected override async Task PerformInitialization()
        {
            if (NeedsInitialize)
            {
                NeedsInitialize = false;
                await Database.EnsureCreatedAsync();
            }
        }
        public override async Task EnsureGlobalIdentityStart()
        {
        }
    }


    // expected to reset multiple times per instance run
    public class TestStorage(IQueryManager service, DbContextOptions<TestStorage> ctx) : TranslationContext(service, ctx)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            _ = options.UseInMemoryDatabase("TestShell");
            _ = options.ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }

        public override async Task EnsureGlobalIdentityStart()
        {
        }

        protected override async Task PerformInitialization()
        {
            if (NeedsInitialize)
            {
                NeedsInitialize = false;
                await Database.EnsureCreatedAsync();
            }
        }
    }

    public class KeepAlive(string conn) : SqliteConnection(conn)
    {
        public override void Close()
        {

        }

        public override Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {

        }
    }


}
