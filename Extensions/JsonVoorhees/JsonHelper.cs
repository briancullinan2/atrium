
namespace Extensions.JsonVoorhees;

public static partial class JsonExtensions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };
}
