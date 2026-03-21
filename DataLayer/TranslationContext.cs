using DataLayer;
using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace DataLayer
{
    // This context never connects to a DB; it just holds your Entity mappings
    public class TranslationContext(DbContextOptions ctx) : DbContext(ctx)
    {
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

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);
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

            _ = Task.Run(async () =>
            {
                var conn = Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                _ = Database.EnsureCreated();
                EnsureGlobalIdentityStart();
                _ = SaveChanges();
            });
        }


        public void EnsureGlobalIdentityStart()
        {
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

    /*
    public class WrapperInterceptor : IMaterializationInterceptor
    {
        public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
        {
            // If it's one of our entities, wrap it in the Smart Proxy
            if (instance is IEntity entity)
            {
                var serviceProvider = materializationData.Context.GetService<IServiceProvider>();
                var result = Entity.Wrap(entity, serviceProvider) ?? throw new InvalidOperationException("Failed to wrap object: " + instance);
                return result;
            }
            return instance;
        }
    }
    */


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
    public class PersistentStorage(DbContextOptions<PersistentStorage> ctx) : TranslationContext(ctx)
    {
    }


    // expected to reset once at the beginning of application load
    public class EphemeralStorage(DbContextOptions<EphemeralStorage> ctx) : TranslationContext(ctx)
    {
    }


    // default interface between web client and http host server
    public class RemoteStorage(DbContextOptions<RemoteStorage> ctx) : TranslationContext(ctx)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            _ = options.UseInMemoryDatabase("RemoteShell");

#pragma warning disable EF1001 // Internal EF Core API usage.
            _ = options.ReplaceService<IQueryCompiler, Utilities.RemoteQuery>();
#pragma warning restore EF1001 // Internal EF Core API usage.
        }
    }


    // expected to reset multiple times per instance run
    public class TestStorage(DbContextOptions<TestStorage> ctx) : TranslationContext(ctx)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            _ = options.UseInMemoryDatabase("TestShell");
        }
    }
}
