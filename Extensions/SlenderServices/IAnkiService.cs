
namespace Extensions.SlenderServices;

public interface IAnkiService
{
    Task<Tuple<IEnumerable<object>?, IEnumerable<object>?>> InspectFile(string ankiPackage);
    Task<IEnumerable<object>?> Search(string? term);
    Task<IEnumerable<object>?> Download(string? ankiPackage);
}
