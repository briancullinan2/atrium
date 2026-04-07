

namespace DataStore.Services;

internal static class QueryService //: QueryManager
{


    // TODO: actually hook this up in WebServer
    public async static Task<object?> RespondSave(Stream body, IQueryManager Query)
    {
        string? rawXml = JsonSerializer.Deserialize<string>(body);

        if (string.IsNullOrWhiteSpace(rawXml))
        {
            throw new InvalidOperationException("Query not received, try again, or not at all, I don't care.");
        }

        // TODO: add marshalling rules here
        var uploadedEntity = (Query.ToExpression(rawXml) as ConstantExpression)?.Value as IEntity
            ?? throw new InvalidOperationException("Failed to render input entity, try again or don't.");

        var result = await Query.Save(uploadedEntity);

        return result;
    }




    public async static Task<object?> RespondQuery(Stream body, IQueryManager Query)
    {
        string? rawXml = JsonSerializer.Deserialize<string>(body);

        if (string.IsNullOrWhiteSpace(rawXml))
        {
            throw new InvalidOperationException("Query not received, try again, or not at all, I don't care.");
        }

        // TODO: add marshalling rules here
        var results = await Query.ToQueryable(rawXml);

        return results;
    }



}
