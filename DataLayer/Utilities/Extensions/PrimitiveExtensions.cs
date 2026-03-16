using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DataLayer.Utilities.Extensions
{
    public static class PrimitiveExtensions
    {

        public static string ToSafe(this string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            string[] words = Regex.Split(url, @"[^a-zA-Z0-9]+", RegexOptions.IgnoreCase);
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;

            var titleCasedWords = words
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => ti.ToTitleCase(w.ToLower()));

            string result = string.Join("", titleCasedWords);
            return result.Substring(0, Math.Min(result.Length, 100));
        }


        public static TEnum? TryParse<TEnum>(this string val) where TEnum : struct, Enum
        {
            return TryParse<TEnum>((object)val);
        }

        // fuck i forget about this: An object reference is required for the non-static field, method, or property 'PrimitiveExtensions.TryParse<PackMode>(PackMode, object)'
        //public static TEnum? TryParse<TEnum>(this TEnum type, object val) where TEnum : struct, Enum
        //{
        //    return TryParse<TEnum>(val);
        //}

        public static TEnum? TryParse<TEnum>(object val) where TEnum : struct, Enum
        {
            if (val is int love || int.TryParse(val.ToString(), out love))
            {
                return (TEnum?)Enum.ToObject(typeof(TEnum), love);
            }

            if (val is string love2 && Enum.TryParse<TEnum>(val.ToString(), out var love3))
            {
                return (TEnum?)love3;
            }

            foreach (var value in Enum.GetValues<TEnum>())
            {
                var attribute = typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>();
                if (attribute != null && string.Equals(val.ToString(), Enum.GetName(value)) || string.Equals(val.ToString(), attribute?.Description, StringComparison.InvariantCultureIgnoreCase))
                {
                    return (TEnum?)Enum.ToObject(typeof(Enum), value);
                }
            }

            return null;
        }
    }
}
