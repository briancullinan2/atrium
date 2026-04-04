namespace AnkiParser
{

    public class Inspection
    {
        public List<File>? Files { get; set; }
        public List<Card>? Cards { get; set; }

    }

    public interface IAnkiService
    {
        Task<Tuple<IEnumerable<File>?, IEnumerable<Card>?>> InspectFile(string ankiPackage);
        Task<IEnumerable<File>?> Search(string? term);
        Task<IEnumerable<File>?> Download(string? ankiPackage);
    }
}
