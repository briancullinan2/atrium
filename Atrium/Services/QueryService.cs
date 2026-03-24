using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;

#if WINDOWS
using Microsoft.AspNetCore.Http;
using System.Linq.Expressions;

#endif
using System.Text.Json;

namespace Atrium.Services
{
    internal class QueryService : QueryManager
    {

#if WINDOWS
        public async static Task RespondQuery(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                string? rawXml = JsonSerializer.Deserialize<string>(jsonQuery);

                if (string.IsNullOrWhiteSpace(rawXml))
                {
                    throw new InvalidOperationException("Query not received, try again, or not at all, I don't care.");
                }

                // TODO: add marshalling rules here

                var results = await DataLayer.Utilities.Extensions.LinqExtensions.ToQueryable(rawXml);

                context.Response.ContentType = "application/json";
                var xml = Expression.Constant(results).ToXDocument().ToString();
                var json = JsonSerializer.Serialize(xml, JsonHelper.Default);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = 500;
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
