using DataStore.Providers;

namespace AnkiParser;

public class TranslationContext(string tempPath, IQueryManager query, DbContextOptions<TranslationContext> ctx) : SqliteTranslationContext<IEntity>(query, ctx)
{
    public override IQueryManager Query { get; set; } = query;
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        base.OnConfiguring(options);// TODO: ??// options.AddInterceptors(new WrapperInterceptor());
        options.UseSqlite($"Data Source={tempPath}");
    }
}
