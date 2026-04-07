
using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace Extensions.PrometheusTypes;

public static partial class TypeExtensions
{


    // Token: 0x060002F6 RID: 758 RVA: 0x00019200 File Offset: 0x00017400
    public static bool IsLocked(this object o)
    {
        bool result;
        if (!Monitor.TryEnter(o))
        {
            result = true;
        }
        else
        {
            Monitor.Exit(o);
            result = false;
        }
        return result;
    }




    private static readonly Type[] IterableBaseTypes =
    [
        typeof(IEnumerable<>),
        typeof(IAsyncEnumerable<>),
        typeof(IEnumerable) // Catch-all for non-generic collections like ArrayList
    ];

    public static bool IsIterable(this Type type)
    {
        // 1. Strings are technically char sequences, but we treat them as atoms
        if (type == typeof(string)) return false;

        // 2. Arrays are always iterable
        if (type.IsArray) return true;

        // 3. Check the Type itself if it's an open or closed generic of our bases
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (IterableBaseTypes.Any(baseType => baseType == def)) return true;
        }

        // 4. Check the interface hierarchy
        return type.GetInterfaces().Any(i =>
            i == typeof(IEnumerable) ||
            (i.IsGenericType && IterableBaseTypes.Contains(i.GetGenericTypeDefinition()))
        );
    }





    private static readonly NullabilityInfoContext context = new();



    public static bool IsNumeric(this Type type)
    {
        if (type == null) return false;

        // Handle Nullable<T> by getting the underlying type (e.g., int? -> int)
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType.IsPrimitive)
        {
            return underlyingType != typeof(bool) &&
                   underlyingType != typeof(char) &&
                   underlyingType != typeof(IntPtr) &&
                   underlyingType != typeof(UIntPtr);
        }

        // Handle types that are numeric but not "Primitive" in .NET terms
        return underlyingType == typeof(decimal);
    }

    public static bool IsNumeric(this MemberInfo member)
    {
        return member switch
        {
            PropertyInfo prop => prop.PropertyType.IsNumeric(),
            FieldInfo field => field.FieldType.IsNumeric(),
            MethodInfo method => method.ReturnType.IsNumeric(),
            Type type => type.IsNumeric(), // Handle the case where the member IS a type
            _ => throw new InvalidOperationException($"Member type {member.GetType().Name} is not supported.")
        };
    }



    public static bool IsSimple(this Type anyType)
    {
        var type = Nullable.GetUnderlyingType(anyType) ?? anyType;
        return type.IsPrimitive ||
            type.IsEnum ||
            new[] {
        typeof(string),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid)
            }.Contains(type) ||
            Convert.GetTypeCode(type) != TypeCode.Object ||
            (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(Nullable<>)
                && type.GetGenericArguments().FirstOrDefault()?.IsSimple() == true);
    }




    public static bool IsConcrete(this Type type)
    {
        if (type == null) return false;

        return !type.IsAbstract &&
               !type.IsInterface &&
               !type.IsGenericTypeDefinition;
    }



    public static bool ReadOnly(this PropertyInfo property)
    {
        return !property.IsWritable();
    }


    public static bool IsWritable(this PropertyInfo property)
    {
        return property.CanWrite && property.GetSetMethod(nonPublic: true) != null;
    }


    public static bool Interfaces(this Type componentType, Type interfaceType)
    {
        // 1. Get all properties defined in the interface
        var interfaceProps = interfaceType.GetProperties(null);

        foreach (var iProp in interfaceProps)
        {
            // 2. Check if the component has a property with the same name and type
            var cProp = componentType.GetProperties(iProp.Name).FirstOrDefault();

            if (cProp == null) return false; // Missing property
            if (iProp.PropertyType.Extends(cProp.PropertyType) != true) return false; // Type mismatch

            // 3. Optional: Ensure the component property is a [Parameter]
            if (!cProp.IsDefined(typeof(ParameterAttribute), true)) return false;
        }

        return true;
    }

}
