using DataLayer.Customization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkiParser
{
    public static class Parser
    {

        public static List<DataLayer.Entities.File> ListFiles(string ankiPackage)
        {
            if (!File.Exists(ankiPackage))
            {
                throw new InvalidOperationException("Anki file doesn't exist");
            }
            var results = new List<DataLayer.Entities.File>();
            var zipStream = File.OpenRead(ankiPackage);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                results.Add(new DataLayer.Entities.File()
                {
                    Filename = entry.FullName,
                    Source = Path.GetFileName(ankiPackage)
                });
            }

            return results;
        }

        public static List<DataLayer.Entities.Card> ParseCards(string ankiPackage)
        {
            if (!File.Exists(ankiPackage))
            {
                throw new InvalidOperationException("Anki file doesn't exist");
            }
            var results = new List<DataLayer.Entities.File>();
            var zipStream = File.OpenRead(ankiPackage);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".anki2"))
                {
                    return ParseCards(entry.Open());
                }
            }
            return [];
        }

        public static IEnumerable<DataLayer.Entities.Card> ParseCards(Stream anki2Database)
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
            var models = JsonSerializer.Deserialize<Dictionary<long, AnkiModel>>(collection.NoteTypes);

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

                var template = model.Tmpls.FirstOrDefault(t => t.Ord == card.Ordinal);
                if (template == null) continue;

                // 4. Map to Study Sauce Entity
                results.Add(new DataLayer.Entities.Card()
                {
                    // Inject the field values into the Mustache brackets
                    Content = ReplaceAnkiTags(template.QFmt, model.Flds, fieldValues),
                    ResponseContent = ReplaceAnkiTags(template.AFmt, model.Flds, fieldValues),

                    Created = DateTimeOffset.FromUnixTimeSeconds(card.ModifiedTimestamp).UtcDateTime,
                    Recurrence = $"{card.Interval} days",
                    PackId = (int)card.DeckId, // Mapping Anki Deck to Sauce Pack
                    ContentType = template.QFmt.Contains("{{Image}}") ? DisplayType.Image : DisplayType.Text
                });
            }
            uploadConn.Close();
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

    }
    public class AnkiModel
    {
        public List<AnkiField> Flds { get; set; }
        public List<AnkiTemplate> Tmpls { get; set; }
    }

    public class AnkiField
    {
        public string Name { get; set; }
        public int Ord { get; set; }
    }

    public class AnkiTemplate
    {
        public string Name { get; set; }
        public string QFmt { get; set; } // Question Format (Front)
        public string AFmt { get; set; } // Answer Format (Back)
        public int Ord { get; set; }
    }
}
