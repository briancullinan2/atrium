using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataLayer.Utilities.Extensions
{
    public static partial class PrimitiveExtensions
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



        public static object? TryParse(this string val, Type enumType)
        {
            return typeof(PrimitiveExtensions)
                .GetMethod(nameof(TryParse), [typeof(object)])
                ?.MakeGenericMethod(enumType)
                .Invoke(null, [val]);
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

            // ignore case because comes from a url
            if (val is string && Enum.TryParse<TEnum>(val.ToString(), true, out var love3))
            {
                return (TEnum?)love3;
            }

            foreach (var value in Enum.GetValues<TEnum>())
            {
                if (string.Equals(value.ToString(), val.ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    return (TEnum?)Enum.ToObject(typeof(TEnum), value);
                }
                var attribute = typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>();
                if (attribute != null && string.Equals(val.ToString(), Enum.GetName(value)) || string.Equals(val.ToString(), attribute?.Description, StringComparison.InvariantCultureIgnoreCase))
                {
                    return (TEnum?)Enum.ToObject(typeof(TEnum), value);
                }
            }

            return null;
        }

        [GeneratedRegex(@"[^a-zA-Z0-9]+", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex SafeRegex();
    }
}
