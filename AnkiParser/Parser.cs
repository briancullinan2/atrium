using DataLayer.Customization;
using DataLayer.Utilities.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkiParser
{
    public static class Parser
    {

        public static List<DataLayer.Entities.File> ListFiles(string? ankiPackage, IServiceProvider _services)
        {
            if (!File.Exists(ankiPackage))
            {
                throw new InvalidOperationException("Anki file doesn't exist");
            }

            var results = new List<DataLayer.Entities.File>();

            var fileTime = File.GetLastWriteTime(ankiPackage);
            var simpleName = Path.GetFileName(ankiPackage).ToSafe();

            // idempotence
            using var scope = _services.CreateScope();
            var persistentStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
            using var context = persistentStore.CreateDbContext();
            if (context.Files.Any(f => f.Created == fileTime && f.Filename == ankiPackage))
            {
                results = context.Files.Where(f => f.Source == simpleName).ToList();
                if (results.Any())
                {
                    return results;
                }
            }

            // Wrapping in using ensures the file is unlocked even on error
            using var zipStream = File.OpenRead(ankiPackage);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            //var firstTime = archive.Entries.FirstOrDefault()?.LastWriteTime.DateTime;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var newFile = new DataLayer.Entities.File()
                {
                    Filename = entry.FullName,
                    Source = simpleName,
                    // Accessing the DateTime component of the DateTimeOffset
                    Created = entry.LastWriteTime.DateTime
                };
                results.Add(newFile);
                var wrapped = DataLayer.Entities.Entity.Wrap(newFile, _services, typeof(IDbContextFactory<DataLayer.EphemeralStorage>));
                wrapped.Save();
            }

            return results;
        }

        public static List<DataLayer.Entities.Card> ParseCards(string? ankiPackage, IServiceProvider _services)
        {
            if (!File.Exists(ankiPackage))
            {
                throw new InvalidOperationException("Anki file doesn't exist");
            }

            var fileTime = File.GetLastWriteTime(ankiPackage);
            var simpleName = Path.GetFileName(ankiPackage).ToSafe();

            // idempotence
            using var scope = _services.CreateScope();
            var persistentStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
            using var context = persistentStore.CreateDbContext();
            var results = context.Cards.Where(f => f.Source == simpleName).ToList();
            if (results.Any() == true)
            {
                return results;
            }




            var zipStream = File.OpenRead(ankiPackage);
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
                    return ParseCards(entry.Open(), simpleName, _services);
                }
            }
            return [];
        }

        private static List<DataLayer.Entities.Card> ParseCards(Stream anki2Database, string source, IServiceProvider _services)
        {
            var tempPath = Path.GetTempFileName();
            using (var fs = File.OpenWrite(tempPath)) { anki2Database.CopyTo(fs); fs.Close(); }
            anki2Database.Close();

            using var uploadConn = new SqliteConnection($"Data Source={tempPath}");
            uploadConn.Open();
            //uploadConn.BackupDatabase(connection);

            var options = new DbContextOptionsBuilder<TranslationContext>()
                    .UseSqlite(uploadConn)
                    .Options;

            using var context = new TranslationContext(options);

            // 1. Get the Note Models (Col.models JSON) 
            // Anki stores deck configs and note models in the 'col' table 'models' column
            var collection = context.Collections.First();
            var models = JsonSerializer.Deserialize<Dictionary<long, AnkiModel>>(collection.NoteTypes, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });

            var results = new List<DataLayer.Entities.Card>();
            var cards = context.Cards.Include(c => c.Note).ToList();

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

                var newCard = new DataLayer.Entities.Card()
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
                };
                results.Add(newCard);
                // idempotence
                var wrapped = DataLayer.Entities.Entity.Wrap(newCard, _services, typeof(IDbContextFactory<DataLayer.EphemeralStorage>));
                wrapped.Save();
            }
            uploadConn.Close();
            SqliteConnection.ClearPool(uploadConn);
            File.Delete(tempPath);

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
            return Regex.Replace(output, @"{{.*?}}", "").Trim();
        }


        // Key: Original Name (from flds), Value: Disk Name (the number)
        public static Dictionary<string, string>? NameToDiskMap { get; private set; }

        public static void ParseMediaFile(Stream fileStream)
        {
            using (StreamReader reader = new StreamReader(fileStream))
            {
                string jsonContent = reader.ReadToEnd();

                // Anki's 'media' file is a JSON object: {"0": "img1.png", "1": "img2.jpg"}
                var rawMap = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (rawMap != null)
                {
                    // We flip it so you can look up "Screen Shot..." and get "0"
                    NameToDiskMap = rawMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
                }
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
                int end = input.IndexOf("\"", start);
                return input.Substring(start, end - start);
            }
            return input;
        }
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
