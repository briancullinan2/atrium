
namespace DataShared.Extensions;

internal static partial class JsonExtensions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };
}
