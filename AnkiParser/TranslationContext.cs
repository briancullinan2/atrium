using DataLayer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AnkiParser
{
    public class TranslationContext(string tempPath, DbContextOptions<TranslationContext> ctx) : DbContext(ctx)
    {
        public DbSet<Entities.Collection>? Collections { get; set; }
        public DbSet<Entities.Note>? Notes { get; set; }
        public DbSet<Entities.Card>? Cards { get; set; }
        public DbSet<Entities.Review>? Reviews { get; set; }
        public DbSet<Entities.Grave>? Graves { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);// TODO: ??// options.AddInterceptors(new WrapperInterceptor());
            options.UseSqlite($"Data Source={tempPath}");
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            Database.GetDbConnection().Open();
        }
    }
}
