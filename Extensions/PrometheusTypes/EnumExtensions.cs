using System.Runtime.Serialization;

namespace Extensions.PrometheusTypes;

// the point of this is simple, Enum.TryParse have an out parameter, 
//   it requires a type to get a type, but if i do string.TryParse(Enum) i can
//   get an object? that i can cast and check later or left hand assignment
public static class EnumExtensions
{

    public static string GetDisplayText(this Enum enumValue)
    {
        var type = enumValue.GetType();
        var memberName = enumValue.ToString();
        var member = type.GetMember(memberName).FirstOrDefault();
        if (member == null) return memberName;

        // 1. Check [Display(Description = "")] - Highest priority for modern .NET
        var display = member.GetCustomAttributes<DisplayAttribute>().FirstOrDefault();
        if (!string.IsNullOrEmpty(display?.GetDescription())) return display.GetDescription()!;

        // 2. Check [EnumMember(Value = "")] - Priority for Cloud/Serialization
        var enumMember = member.GetCustomAttributes<EnumMemberAttribute>().FirstOrDefault();
        if (!string.IsNullOrEmpty(enumMember?.Value)) return enumMember.Value;

        // 3. Check [Description("")] - Standard legacy WPF/WinForms support
        var description = member.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault();
        if (!string.IsNullOrEmpty(description?.Description)) return description.Description;

        // Fallback: The raw string (munged or otherwise)
        return memberName;
    }



    public static object? TryParse(this string val, Type enumType)
    {
        return typeof(EnumExtensions)
            .GetMethod(nameof(EnumExtensions.TryParse), [typeof(object)])
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
            var attribute = typeof(TEnum).GetField(value.ToString())?.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault();
            if (attribute != null && string.Equals(val.ToString(), Enum.GetName(value)) || string.Equals(val.ToString(), attribute?.Description, StringComparison.InvariantCultureIgnoreCase))
            {
                return (TEnum?)Enum.ToObject(typeof(TEnum), value);
            }
        }

        return null;
    }

}
