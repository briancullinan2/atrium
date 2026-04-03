
using Microsoft.EntityFrameworkCore.Storage;
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {


        private static object? ResolveMetadata(Type targetType, string? value, XElement el)
        {
            if (targetType == typeof(Type) && value != null)
                return Type.GetType(value);

            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(ExpressionType) && value != null)
                return Enum.Parse<ExpressionType>(value);

            if (targetType == typeof(MethodInfo))
                return RebuildMember<MethodInfo>(el);


            if (targetType == typeof(PropertyInfo))
                return RebuildMember<PropertyInfo>(el);


            if (targetType == typeof(ValueBuffer)
                || (typeof(IEnumerable).IsAssignableFrom(targetType)
                    && targetType != typeof(string)))
            {
                var typeArgument = targetType.GetGenericArguments().FirstOrDefault();

                var values = el.Elements().Select(e =>
                {
                    if (e.Name == "Null") return null;
                    var typeName = e.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                        ?? throw new InvalidOperationException("Could not resolve type name on: " + e + " on " + el);
                    var entityType = Type.GetType(typeName)
                        ?? typeArgument
                        ?? throw new InvalidOperationException("Could not resolve type on: " + e + " on " + el);
                    if (e.Attribute("Value")?.Value == "Null")
                        return null;
                    else if (entityType.IsSimple())
                        return Convert.ChangeType(e.Attribute("Value")?.Value, entityType);
                    else
                        return ResolveMetadata(entityType, e.Attribute("Value")?.Value, e);
                }).ToArray();

                var castedValues = CollectionConverter.ConvertAsync(values, targetType);

                // Fallback for other IEnumerables
                return castedValues;
            }

            if (typeof(IEntity).IsAssignableFrom(targetType))
            {
                var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var entity = Activator.CreateInstance(targetType);
                foreach (var prop in props)
                {
                    if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                        continue;

                    if (prop.ReadOnly())
                        continue;

                    var baseType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    if ((el.Attribute(prop.Name)?.Value == "Null"
                        || el.Element(prop.Name)?.Attribute("Value")?.Value == "Null")
                        && prop.IsNullable())
                    {
                        prop.SetValue(entity, null);
                    }

                    else if (baseType.IsIterable()
                        && el.Element(prop.Name) is XElement complex)
                    {
                        var complexValue = ResolveMetadata(baseType, el.Attribute(prop.Name)?.Value, complex);
                        var convertedValue = CollectionConverter.ConvertAsync(complexValue, baseType);
                        if (convertedValue?.GetType().Extends(baseType) != true)
                            throw new InvalidOperationException("Iterable types don't match: " + complexValue?.GetType() + " -> " + convertedValue?.GetType() + " and " + baseType + " on " + el + " property " + prop);
                        prop.SetValue(entity, convertedValue);
                    }

                    else if (baseType.IsSimple())
                    {
                        var converter = TypeDescriptor.GetConverter(baseType);
                        object? simpleValue = null;
                        if (baseType == typeof(string))
                            simpleValue = el.Attribute(prop.Name)?.Value ?? string.Empty;
                        else if (!string.IsNullOrWhiteSpace(el.Attribute(prop.Name)?.Value))
                            simpleValue = converter.ConvertFromString(el.Attribute(prop.Name)!.Value);
                        if (!prop.IsNullable() && simpleValue?.GetType() != baseType)
                            throw new InvalidOperationException("Simple types don't match: " + simpleValue?.GetType() + " and " + baseType + " on " + el + " property " + prop);
                        prop.SetValue(entity, simpleValue);
                    }

                    else if (el.Element(prop.Name) is XElement complex2)
                    {
                        var complexValue = ResolveMetadata(baseType, el.Attribute(prop.Name)?.Value, complex2);
                        if (complexValue?.GetType() != baseType)
                            throw new InvalidOperationException("Complex types don't match: " + complexValue?.GetType() + " and " + baseType + " on " + el + " property " + prop);
                        prop.SetValue(entity, complexValue);
                    }

                    else
                        throw new InvalidOperationException("Don't know how to handle property type: " + baseType + " on " + el + " property " + prop
                            + " value is " + (el.Attribute(prop.Name)?.Value ?? el.Element(prop.Name)?.Attribute("Value")?.Value)
                            + " prop type is nullable " + prop.IsNullable());
                }
                return entity;
            }


            throw new InvalidOperationException("Don't know that type: " + targetType);
        }




    }
}
