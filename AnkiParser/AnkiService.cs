


using Microsoft.AspNetCore.Authorization;

namespace AnkiParser;

public partial class AnkiService(HttpClient Http, IFileManager FileManager, IQueryManager Query) : IAnkiService
{

    public async Task<Tuple<IEnumerable<object>?, IEnumerable<object>?>> InspectFile(string ankiPackage)
    {

        try
        {
            var files = await AnkiParser.Parser.ListFiles(ankiPackage, Query);
            var cards = await AnkiParser.Parser.ParseCards(ankiPackage, Query);
            return new Tuple<IEnumerable<object>?, IEnumerable<object>?>(files, cards);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new Tuple<IEnumerable<object>?, IEnumerable<object>?>([], []);
        }
    }

    public async Task<IEnumerable<object>?> Search(string? searchTerm)
    {
        // TODO: add client id gating
        return await SearchAnki("", searchTerm, Http);
    }


    [AllowAnonymous]
    protected static async Task<IEnumerable<object>?> SearchAnki(string clientId, string? searchTerm, HttpClient HttpClient)
    {
        // TODO: add client id gating
        return await TaskExtensions.Debounce(DoActualSearch, 3000, searchTerm, HttpClient);
    }



    [AllowAnonymous]
    public async Task<IEnumerable<object>?> Download(string? ankiPackageUrl)
    {
        if (string.IsNullOrWhiteSpace(ankiPackageUrl))
        {
            throw new InvalidOperationException("Must enter a package name.");
        }


        // ResponseHeadersRead ensures we don't buffer the whole file into RAM first
        var response = await Http.GetAsync(ankiPackageUrl, HttpCompletionOption.ResponseHeadersRead);
        _ = response.EnsureSuccessStatusCode();

        using var remoteStream = await response.Content.ReadAsStreamAsync();

        // We pass the stream directly to our generalized upload function
        // Using the URL's filename as the local path hint
        var fileName = Path.GetFileName(new Uri(ankiPackageUrl).LocalPath);
        Task? task = FileManager.UploadFile(remoteStream, fileName, "AnkiDownloads");
        if (task is Task wait) await wait;

        // Return the entity (you'll likely want to fetch the record created in UploadFile)
        Task<List<File>>? syncTask = Query.Synchronize<File>(f => f.Source == "Upload" || f.Source == "AnkiDownloads");
        if (syncTask is Task wait2) await wait2;
        return syncTask?.Result;
    }


    public class Inspection
    {
        public List<File>? Files { get; set; }
        public List<Card>? Cards { get; set; }

    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\d{8,12}")]
    private static partial System.Text.RegularExpressions.Regex AnkiIdRegex();
}
