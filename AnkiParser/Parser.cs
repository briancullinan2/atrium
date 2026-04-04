using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkiParser
{
    public static partial class Parser
    {

        public static async Task<List<File>> ListFiles(string? ankiPackage, IQueryManager Query)
        {
            if (!System.IO.File.Exists(ankiPackage))
            {
                throw new InvalidOperationException("Anki file doesn't exist");
            }

            var results = new List<File>();

            var fileTime = System.IO.File.GetLastWriteTime(ankiPackage);
            var simpleName = Path.GetFileName(ankiPackage).ToSafe();

            // idempotence
            var alreadyLoaded = await Query.Query<File>(f => f.Created == fileTime && f.Filename == ankiPackage)
                .ToListAsync();
            if (alreadyLoaded.Count != 0)
            {
                results = [.. await Query.Query<File>(f => f.Source == simpleName).ToListAsync()];
                if (results.Count != 0)
                {
                    return results;
                }
            }

            // Wrapping in using ensures the file is unlocked even on error
            using var zipStream = System.IO.File.OpenRead(ankiPackage);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            //var firstTime = archive.Entries.FirstOrDefault()?.LastWriteTime.DateTime;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var newFile = await Query.Save(new File()
                {
                    Filename = entry.FullName,
                    Source = simpleName,
                    // Accessing the DateTime component of the DateTimeOffset
                    Created = entry.LastWriteTime.DateTime
                });
                results.Add(newFile);
            }

            return results;
        }

        public static async Task<List<Card>> ParseCards(string? ankiPackage, IQueryManager Query)
        {
            if (!System.IO.File.Exists(ankiPackage))
            {
                throw new InvalidOperationException("Anki file doesn't exist");
            }

            var fileTime = System.IO.File.GetLastWriteTime(ankiPackage);
            var simpleName = Path.GetFileName(ankiPackage).ToSafe();

            // idempotence
            var alreadyLoaded = await Query.Query<Card>(c => c.Source == simpleName).ToListAsync();
            if (alreadyLoaded.Count != 0)
            {
                return [.. alreadyLoaded];
            }




            var zipStream = System.IO.File.OpenRead(ankiPackage);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("media"))
                {
                    ParseMediaFile(entry.Open());
                }
            }
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".anki2"))
                {
                    return await ParseCards(entry.Open(), simpleName, Query);
                }
            }
            return [];
        }

        private static async Task<List<Card>> ParseCards(Stream anki2Database, string source, IQueryManager Query)
        {
            var tempPath = Path.GetTempFileName();
            using (var fs = System.IO.File.OpenWrite(tempPath)) { anki2Database.CopyTo(fs); fs.Close(); }
            anki2Database.Close();


            using var context = new TranslationContext(tempPath, new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TranslationContext>().Options);
            var connection = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection(context.Database);

            // 1. Get the Note Models (Col.models JSON) 
            // Anki stores deck configs and note models in the 'col' table 'models' column
            var collection = context.Set<Entities.Collection>().First();
            var models = JsonSerializer.Deserialize<Dictionary<long, AnkiModel>>(collection.NoteTypes, JsonExtensions.Default);

            var results = new List<Card>();
            var cards = await EntityFrameworkQueryableExtensions.ToListAsync(context.Set<Entities.Card>().Where(c => c.Note != null));

            foreach (var card in cards)
            {
                if (card.Note == null) continue;

                // 2. Split the fields (Anki uses 0x1f as separator)
                var fieldValues = card.Note.FieldList;

                // 3. Find the Model and the specific Template for this Card
                // card.Note.ModelId is 'mid' in the notes table
                if (models?.TryGetValue(card.Note.ModelId, out var model) != true || model == null) continue;

                var template = model.Tmpls?.FirstOrDefault(t => t.Ord == card.Ordinal);
                if (template == null) continue;

                var newCard = await Query.Save(new Card()
                {
                    // Inject the field values into the Mustache brackets
                    Content = ReplaceAnkiTags(template.QFmt ?? "", model.Flds ?? [], fieldValues),
                    ResponseContent = ReplaceAnkiTags(template.AFmt ?? "", model.Flds ?? [], fieldValues),
                    Tag = GetDiskFilename(ReplaceAnkiTags(template.QFmt ?? "", model.Flds ?? [], fieldValues)),
                    Recurrence = $"{card.Interval} days",
                    Modified = DateTimeOffset.FromUnixTimeSeconds(card.ModifiedTimestamp).UtcDateTime,
                    Source = source,
                    //PackId = (int)card.DeckId, // Mapping Anki Deck to Sauce Pack
                    ContentType = template.QFmt?.Contains("{{Image}}") == true ? DisplayType.Image : DisplayType.Text
                });
                // idempotence
                results.Add(newCard);
            }



            connection.Close();
            if (connection is SqliteConnection sqlite)
                SqliteConnection.ClearPool(sqlite);
            System.IO.File.Delete(tempPath);

            return results;
        }

        // Simple Helper to replace {{FieldName}} with the actual value
        private static string ReplaceAnkiTags(string template, List<AnkiField> fields, string[] values)
        {
            string output = template;
            for (int i = 0; i < fields.Count; i++)
            {
                if (i < values.Length)
                    output = output.Replace("{{" + fields[i].Name + "}}", values[i]);
            }
            // Remove any remaining Anki-specific syntax like {{type:...}} or {{#...}}
            return AnkiTagRegEx().Replace(output, "").Trim();
        }


        // Key: Original Name (from flds), Value: Disk Name (the number)
        public static Dictionary<string, string>? NameToDiskMap { get; private set; }

        public static void ParseMediaFile(Stream fileStream)
        {
            using StreamReader reader = new(fileStream);
            string jsonContent = reader.ReadToEnd();

            // Anki's 'media' file is a JSON object: {"0": "img1.png", "1": "img2.jpg"}
            var rawMap = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

            if (rawMap != null)
            {
                // We flip it so you can look up "Screen Shot..." and get "0"
                NameToDiskMap = rawMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            }
        }

        public static string? GetDiskFilename(string fldsImageSrc)
        {
            // Clean the string in case it has HTML tags or leading pathing
            // Example input: <img src="Screen Shot 2021-06-18 at 11.34.0.png">
            string cleanName = ExtractFilename(fldsImageSrc);

            if (NameToDiskMap?.TryGetValue(cleanName, out string? diskId) == true)
            {
                return diskId;
            }
            return null; // Not found
        }

        private static string ExtractFilename(string input)
        {
            // Simple helper to pull the filename out of an <img> tag if necessary
            if (input.Contains("src=\""))
            {
                int start = input.IndexOf("src=\"") + 5;
                int end = input.IndexOf('\"', start);
                return input[start..end];
            }
            return input;
        }

        [GeneratedRegex(@"{{.*?}}")]
        private static partial Regex AnkiTagRegEx();
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class AnkiModel
    {
        public List<AnkiField>? Flds { get; set; }
        public List<AnkiTemplate>? Tmpls { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class AnkiField
    {
        public string? Name { get; set; }
        public int Ord { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class AnkiTemplate
    {
        public string? Name { get; set; }
        public string? QFmt { get; set; } // Question Format (Front)
        public string? AFmt { get; set; } // Answer Format (Back)
        public int Ord { get; set; }
    }
}
