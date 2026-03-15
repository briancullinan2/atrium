namespace FlashCard.Services
{

    public class Inspection
    {
        public List<DataLayer.Entities.File> Files { get; set; }
        public List<DataLayer.Entities.Card> Cards { get; set; }

    }

    public interface IAnkiService
    {
        Task<Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<DataLayer.Entities.Card>>> InspectFile(string ankiPackage);
        Task<IEnumerable<DataLayer.Entities.File>?> Search(string? term);
        Task<IEnumerable<DataLayer.Entities.File>> Download(string ankiPackage);
    }
}
