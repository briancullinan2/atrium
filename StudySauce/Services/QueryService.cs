using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudySauce.Services
{
    internal static class QueryService
    {
        public async static Task RespondQuery(HttpContext context, IServiceProvider _service)
        {
            try
            {
                // 1. Read the serialized query from the request body
                context.Response.ContentType = "application/json";
                //using var scope = _service.CreateScope();
                //var ephemeralStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
                //using var dbContext = ephemeralStore.CreateDbContext();

                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                string? rawXml = JsonSerializer.Deserialize<string>(jsonQuery);

                if (string.IsNullOrWhiteSpace(rawXml))
                {
                    var json2 = JsonSerializer.Serialize("");
                    await context.Response.WriteAsync(json2);
                    return;
                }

                // TODO: add marshalling rules here
                var results = DataLayer.Utilities.Extensions.LinqExtensions.ToQueryable(rawXml, _service);

                var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });

                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(ex.Message, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                    });
                    await context.Response.WriteAsync(json);
                }
                catch (Exception ex2)
                {
                    try
                    {
                        await context.Response.WriteAsync(ex2.Message);
                    }
                    catch (Exception ex3)
                    {
                        Console.WriteLine(ex3);
                    }
                }
            }
        }
    }
}
