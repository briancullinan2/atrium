
namespace DataStore.Services
{
    internal static class QueryService //: QueryManager
    {



        public async static Task ResponseSave(HttpContext context, IQueryManager Query)
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
                var uploadedEntity = (Query.ToExpression(rawXml) as ConstantExpression)?.Value as IEntity
                    ?? throw new InvalidOperationException("Failed to render input entity, try again or don't.");

                var result = await Query.Save(uploadedEntity);

                context.Response.ContentType = "application/json";
                var xml = Expression.Constant(result).ToXDocument().ToString();
                var json = JsonSerializer.Serialize(xml, JsonExtensions.Default);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = 500;
                    var json = JsonSerializer.Serialize(ex.Message, JsonExtensions.Default);
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




        public async static Task RespondQuery(HttpContext context, IQueryManager Query)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var jsonQuery = await reader.ReadToEndAsync();
                string? rawXml = JsonSerializer.Deserialize<string>(jsonQuery);


                //string testQuery = Path.Combine(AppContext.BaseDirectory, "testQuery.txt");
                //var query = System.IO.File.ReadAllText(testQuery);
                //var testResults = Query.ToExpression(query);
                //Console.WriteLine(testResults);

                if (string.IsNullOrWhiteSpace(rawXml))
                {
                    throw new InvalidOperationException("Query not received, try again, or not at all, I don't care.");
                }

                // TODO: add marshalling rules here
                var results = await Query.ToQueryable(rawXml);



                context.Response.ContentType = "application/json";
                var xml = Expression.Constant(results).ToXDocument().ToString();
                var json = JsonSerializer.Serialize(xml, JsonExtensions.Default);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = 500;
                    var json = JsonSerializer.Serialize(ex.Message, JsonExtensions.Default);
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
