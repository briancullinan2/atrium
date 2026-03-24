using DataLayer.Entities;
using DataLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataLayer
{
    // This context never connects to a DB; it just holds your Entity mappings
    public class TranslationContext(IServiceProvider service, DbContextOptions ctx) : DbContext(ctx)
    {
        public IServiceProvider Service { get; set; } = service;
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

        public bool NeedsInitialize { get; protected set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);
            options.AddInterceptors(new WrapperInterceptor());
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



        public virtual async Task InitializeIfNeeded()
        {
            IDbContextTransaction? transaction = null;
            try
            {
                transaction = await Database.BeginTransactionAsync();
                if (!NeedsInitialize) return;
                var conn = Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                await Database.EnsureCreatedAsync();
                await EnsureGlobalIdentityStart();
                await SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                if(transaction != null) await transaction.RollbackAsync();
                throw new InvalidOperationException("Database creation failed.", ex);
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }




        public virtual async Task EnsureGlobalIdentityStart()
        {
            Log.Info("Inserting 0 IDs.");

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

    public class WrapperInterceptor() : IMaterializationInterceptor
    {
        public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
        {
            // If it's one of our entities, wrap it in the Smart Proxy
            if (instance is IEntity entity)
            {
                var serviceProvider = materializationData.Context.GetService<IServiceProvider>();
                entity.Service = serviceProvider;
                entity.ContextType = materializationData.Context.GetType();
                //var result = Entity.Wrap(entity, serviceProvider) ?? throw new InvalidOperationException("Failed to wrap object: " + instance);
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
    public class PersistentStorage(IServiceProvider service, DbContextOptions<PersistentStorage> ctx) : TranslationContext(service, ctx)
    {
    }


    // expected to reset once at the beginning of application load
    public class EphemeralStorage(IServiceProvider service, DbContextOptions<EphemeralStorage> ctx) : TranslationContext(service, ctx)
    {
    }


    // default interface between web client and http host server
    public class RemoteStorage(IServiceProvider service, DbContextOptions<RemoteStorage> ctx) : TranslationContext(service, ctx)
    {
        public string? BaseAddress { get; set; } = null;
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            _ = options.UseInMemoryDatabase("RemoteShell");
            _ = options.ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
#pragma warning disable EF1001 // Internal EF Core API usage.
            _ = options.ReplaceService<IQueryCompiler, RemoteQuery>();
#pragma warning restore EF1001 // Internal EF Core API usage.
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


        public override async Task InitializeIfNeeded()
        {
            if (NeedsInitialize)
            {
                NeedsInitialize = false;
                var conn = Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                await Database.EnsureCreatedAsync();
            }
        }
        public override async Task EnsureGlobalIdentityStart()
        {
        }
    }


    // expected to reset multiple times per instance run
    public class TestStorage(IServiceProvider service, DbContextOptions<TestStorage> ctx) : TranslationContext(service, ctx)
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

        public override async Task InitializeIfNeeded()
        {
            if (NeedsInitialize)
            {
                NeedsInitialize = false;
                var conn = Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                await Database.EnsureCreatedAsync();
            }
        }
    }
}
