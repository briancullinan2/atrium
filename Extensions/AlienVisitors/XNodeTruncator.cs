using System.Xml.Linq;

namespace Extensions.AlienVisitors;


public class XNodeTruncator
{
    public static XDocument Truncate(XDocument doc) =>
        new(doc.Declaration, Truncate(doc.Root!));

    private static XElement Truncate(XElement el)
    {
        try
        {
            return new XElement(el.Name.LocalName[..Math.Min(3, el.Name.LocalName.Length)],
                el.Attributes().Select(a => new XAttribute(
                    a.Name.LocalName[..Math.Min(3, a.Name.LocalName.Length)],
                    a.Value?[..Math.Min(3, a.Value?.Length ?? 0)] ?? string.Empty)).DistinctBy(attr => attr.Name),
                el.Elements().Select(Truncate),
                el.HasElements ? null : el.Value?[..Math.Min(3, el.Value?.Length ?? 0)] ?? string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
}
