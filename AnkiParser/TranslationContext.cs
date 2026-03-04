using Microsoft.EntityFrameworkCore;

namespace AnkiParser
{
    public class TranslationContext : DbContext
    {
        public DbSet<AnkiParser.Entities.Collection> Collections { get; set; }
        public DbSet<AnkiParser.Entities.Note> Notes { get; set; }
        public DbSet<AnkiParser.Entities.Card> Cards { get; set; }
        public DbSet<AnkiParser.Entities.Review> Reviews { get; set; }
        public DbSet<AnkiParser.Entities.Grave> Graves { get; set; }
        public TranslationContext(DbContextOptions<TranslationContext> ctx) : base(ctx)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);
            // TODO: ??
            // options.AddInterceptors(new WrapperInterceptor());

        }


        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }




    }
}
