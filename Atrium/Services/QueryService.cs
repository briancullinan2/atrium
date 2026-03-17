using DataLayer.Utilities;
#if WINDOWS
using Microsoft.AspNetCore.Http;
#endif
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atrium.Services
{
    internal class QueryService : QueryManager
    {

#if WINDOWS
        public async static Task RespondQuery(HttpContext context, IServiceProvider _service)
        {
            try
            {
                context.Response.ContentType = "application/json";

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

                var json = JsonSerializer.Serialize(results, JsonHelper.Default);

                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(ex.Message, JsonHelper.Default);
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
#endif



    }

}
