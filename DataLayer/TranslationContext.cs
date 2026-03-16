using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DataLayer
{
    // This context never connects to a DB; it just holds your Entity mappings
    public class TranslationContext : DbContext
    {
        public DbSet<DataLayer.Entities.Permission> Permissions { get; set; }
        public DbSet<DataLayer.Entities.Role> Roles { get; set; }
        public DbSet<DataLayer.Entities.User> Users { get; set; }
        public DbSet<DataLayer.Entities.Setting> Settings { get; set; }
        public DbSet<DataLayer.Entities.Message> Messages { get; set; }
        public DbSet<DataLayer.Entities.Pack> Packs { get; set; }
        public DbSet<DataLayer.Entities.Card> Cards { get; set; }
        public DbSet<DataLayer.Entities.Answer> Answers { get; set; }
        public DbSet<DataLayer.Entities.Group> Groups { get; set; }
        public DbSet<DataLayer.Entities.File> Files { get; set; }
        public DbSet<DataLayer.Entities.Visit> Visits { get; set; }
        public DbSet<DataLayer.Entities.Session> Sessions { get; set; }
        public DbSet<DataLayer.Entities.Schedule> Schedules { get; set; }
        public DbSet<DataLayer.Entities.Subject> Subjects { get; set; }
        public DbSet<DataLayer.Entities.Grade> Grades { get; set; }
        public DbSet<DataLayer.Entities.Lesson> Lessons { get; set; }


        // Add other entities here...

        public TranslationContext(DbContextOptions ctx) : base(ctx)
        {
        }

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
            //options.AddInterceptors(new WrapperInterceptor());
        }



        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // This makes the conversion "implicit" for the database layer globally.
            configurationBuilder.Properties<Customization.DisplayType>().HaveConversion<int>();
            configurationBuilder.Properties<Customization.ControlMode>().HaveConversion<int>();
            configurationBuilder.Properties<Customization.Gender>().HaveConversion<int>();
            configurationBuilder.Properties<Customization.PackMode>().HaveConversion<int>();
            configurationBuilder.Properties<Customization.PackStatus>().HaveConversion<int>();
            configurationBuilder.Properties<Customization.CardType>().HaveConversion<int>();
            configurationBuilder.Properties<Customization.GradeScale>().HaveConversion<int>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Ignore<System.Text.RegularExpressions.Capture>();
            modelBuilder.Ignore<System.Text.RegularExpressions.Match>();
            modelBuilder.Ignore<System.Text.RegularExpressions.Group>();

            // Explicitly map the Message entity to the "Message" table
            modelBuilder.Entity<Entities.Message>().ToTable(EntityMetadata.Message.TableName);
            modelBuilder.Entity<Entities.Permission>().ToTable(EntityMetadata.Permission.TableName);
            modelBuilder.Entity<Entities.Role>().ToTable(EntityMetadata.Role.TableName);
            modelBuilder.Entity<Entities.Setting>().ToTable(EntityMetadata.Setting.TableName);
            modelBuilder.Entity<Entities.User>().ToTable(EntityMetadata.User.TableName);
            modelBuilder.Entity<Entities.Card>().ToTable(EntityMetadata.Card.TableName);
            modelBuilder.Entity<Entities.Pack>().ToTable(EntityMetadata.Pack.TableName);
            modelBuilder.Entity<Entities.Answer>().ToTable(EntityMetadata.Answer.TableName);
            modelBuilder.Entity<Entities.Group>().ToTable(EntityMetadata.Group.TableName);
            modelBuilder.Entity<Entities.File>().ToTable(EntityMetadata.File.TableName);
            modelBuilder.Entity<Entities.Visit>().ToTable(EntityMetadata.Visit.TableName);
            modelBuilder.Entity<Entities.Session>().ToTable(EntityMetadata.Session.TableName);
            modelBuilder.Entity<Entities.Subject>().ToTable(EntityMetadata.Subject.TableName);
            modelBuilder.Entity<Entities.Schedule>().ToTable(EntityMetadata.Schedule.TableName);
            modelBuilder.Entity<Entities.Grade>().ToTable(EntityMetadata.Grade.TableName);
            modelBuilder.Entity<Entities.Lesson>().ToTable(EntityMetadata.Lesson.TableName);

            /*
            modelBuilder.Entity<User>()
                .Property(u => u.FirstName)
                .HasMaxLength(50); // This is the "Gold Standard" for EF
            */

            /*
            modelBuilder.Entity<MedicalMessage>()
                .Property(e => e.VoltageSetting)
                .HasConversion<int>();
            */

        }

    }
    public class WrapperInterceptor : IMaterializationInterceptor
    {
        public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
        {
            // If it's one of our entities, wrap it in the Smart Proxy
            if (instance is IEntity entity)
            {
                var serviceProvider = materializationData.Context.GetService<IServiceProvider>();
                var result = Entity.Wrap(entity, serviceProvider);
                if(result == null)
                {
                    throw new InvalidOperationException("Failed to wrap object: " + instance);
                }
                return result;
            }
            return instance;
        }
    }
    public partial class EntityMetadata
    {
        public static EntityMetadata<Entities.Answer> Answer => new EntityMetadata<Entities.Answer>();
        public static EntityMetadata<Entities.Pack> Pack => new EntityMetadata<Entities.Pack>();
        public static EntityMetadata<Entities.Card> Card => new EntityMetadata<Entities.Card>();
        public static EntityMetadata<Entities.Permission> Permission => new EntityMetadata<Entities.Permission>();
        public static EntityMetadata<Entities.User> User => new EntityMetadata<Entities.User>();
        public static EntityMetadata<Entities.Role> Role => new EntityMetadata<Entities.Role>();
        public static EntityMetadata<Entities.Setting> Setting => new EntityMetadata<Entities.Setting>();
        public static EntityMetadata<Entities.Message> Message => new EntityMetadata<Entities.Message>();
        public static EntityMetadata<Entities.File> File => new EntityMetadata<Entities.File>();
        public static EntityMetadata<Entities.Group> Group => new EntityMetadata<Entities.Group>();
        public static EntityMetadata<Entities.Visit> Visit => new EntityMetadata<Entities.Visit>();
        public static EntityMetadata<Entities.Session> Session => new EntityMetadata<Entities.Session>();
        public static EntityMetadata<Entities.Subject> Subject => new EntityMetadata<Entities.Subject>();
        public static EntityMetadata<Entities.Schedule> Schedule => new EntityMetadata<Entities.Schedule>();
        public static EntityMetadata<Entities.Grade> Grade => new EntityMetadata<Entities.Grade>();
        public static EntityMetadata<Entities.Lesson> Lesson => new EntityMetadata<Entities.Lesson>();

    }

    public class PersistentStorage : TranslationContext
    {
        public PersistentStorage(DbContextOptions<PersistentStorage> ctx) : base(ctx)
        {
        }

    }

    public class EphemeralStorage : TranslationContext
    {
        public EphemeralStorage(DbContextOptions<EphemeralStorage> ctx) : base(ctx)
        {
        }

    }
}
