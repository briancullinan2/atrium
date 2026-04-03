using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;

namespace Extensions.PrometheusTypes
{
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
            var display = member.GetCustomAttribute<DisplayAttribute>();
            if (!string.IsNullOrEmpty(display?.GetDescription())) return display.GetDescription()!;

            // 2. Check [EnumMember(Value = "")] - Priority for Cloud/Serialization
            var enumMember = member.GetCustomAttribute<EnumMemberAttribute>();
            if (!string.IsNullOrEmpty(enumMember?.Value)) return enumMember.Value;

            // 3. Check [Description("")] - Standard legacy WPF/WinForms support
            var description = member.GetCustomAttribute<DescriptionAttribute>();
            if (!string.IsNullOrEmpty(description?.Description)) return description.Description;

            // Fallback: The raw string (munged or otherwise)
            return memberName;
        }
    }
}
