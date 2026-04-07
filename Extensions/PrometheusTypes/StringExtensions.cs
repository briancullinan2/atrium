
using System.Globalization;

namespace Extensions.PrometheusTypes;

public static partial class StringExtensions
{
    public static string ToCamelCase(this string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return JsonNamingPolicy.CamelCase.ConvertName(name);
    }


    public static bool IsEmpty(this object? obj)
    {
        if (obj is not IEnumerable enumerable || obj is string)
            return true;

        // 1. Check for ICollection first (Fastest - no enumeration)
        if (obj is ICollection collection)
            return collection.Count == 0;

        // 2. The "Nuclear" check for all other IEnumerables (Iterators)
        var enumerator = enumerable.GetEnumerator();
        try
        {
            return !enumerator.MoveNext();
        }
        finally
        {
            // Some iterators implement IDisposable; don't leave them hanging
            (enumerator as IDisposable)?.Dispose();
        }
    }


    public static string ToSafe(this string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        string[] words = SafeRegex().Split(url);
        TextInfo ti = CultureInfo.CurrentCulture.TextInfo;

        var titleCasedWords = words
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => ti.ToTitleCase(w.ToLower()));

        string result = string.Join("", titleCasedWords);
        return result[..Math.Min(result.Length, 100)];
    }


    private static readonly string[] TrueValues = ["true", "1", "yes", "y", "on", "checked"];
    private static readonly string[] FalseValues = ["false", "0", "no", "n", "off", "unchecked"];

    public static bool? TryParse(this string val)
    {
        if (val == null) return null;

        // 1. Check for actual boolean or numeric types first (The 'int love' logic)
        if (bool.TryParse(val, out var b)) return b;

        var inputString = val.ToString()?.Trim();
        if (string.IsNullOrEmpty(inputString)) return null;

        if (int.TryParse(inputString, out int intVal))
        {
            if (intVal == 1) return true;
            if (intVal == 0) return false;
        }

        // 2. Loop through True definitions (Case-Insensitive)
        foreach (var t in TrueValues)
        {
            if (string.Equals(t, inputString, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 3. Loop through False definitions (Case-Insensitive)
        foreach (var f in FalseValues)
        {
            if (string.Equals(f, inputString, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return null; // No match found, just like the Enum fallback
    }


    public static string Limit(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
            return string.Empty;

        if (value.Length <= maxLength)
            return value;

        // If the field is too small for "...", just hard truncate
        if (maxLength <= 3)
            return value[..maxLength];

        // Standard truncation with suffix
        return value[..(Math.Min(value.Length, maxLength) - 3)] + "...";
    }


    public static Dictionary<string, string> Query(this string url)
    {
        var uri = new Uri(url);
        return Query(uri);
    }

    public static Dictionary<string, string> Query(this Uri uri)
    {
        // Use a simple split/regex to avoid heavy libraries
        var query = uri.Query.TrimStart('?');
        var parameters = query.Split('&')
                              .Select(p => p.Split('='))
                              .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");

        return parameters;
    }





    [GeneratedRegex(@"[^a-zA-Z0-9]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SafeRegex();
}
