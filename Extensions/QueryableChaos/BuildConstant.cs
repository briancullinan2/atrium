
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {


        private static ConstantExpression BuildConstant(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var typeName = (el.Element(nameof(ConstantExpression.Type))?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value)
                ?? throw new InvalidOperationException("Could not resolve constant type attribute on: " + el);
            var type = Type.GetType(typeName)
                ?? throw new InvalidOperationException("Could not resolve constant type on: " + el);

            if (el.Element(nameof(ConstantExpression.Value)) is XElement complex)
            {
                // TODO: 
                if (complex.Element("Null") is not null)
                {
                    return Expression.Constant(null, type);
                }
                var val = ResolveMetadata(type, el.Attribute(nameof(ConstantExpression.Value))?.Value, type.IsIterable() ? complex : complex.Elements().FirstOrDefault() ?? complex);
                
                //if (type.GetGenericArguments().FirstOrDefault() == typeof(Visit))
                //{
                //    Console.WriteLine("Made constant: " + JsonSerializer.Serialize(val) + el);
                //}

                if (val?.GetType().IsSimple() == true
                    && val?.GetType() != type)
                {
                    return Expression.Constant(Convert.ChangeType(val, type), type);
                }
                if (type.IsIterable())
                {
                    val = PrometheusTypes.CollectionConverter.ConvertAsync(val, type);
                }

                if (val?.GetType() != type)
                {
                    Console.WriteLine("ArgumentTypesMustMatch is dumbass error that doesn't tell me this type " + val?.GetType() + " or this type: " + type);
                }
                return Expression.Constant(val, type);
            }
            else if (el.Attribute(nameof(ConstantExpression.Value))?.Value is string val)
            {
                if (type == typeof(object) && val == "Null")
                {
                    return Expression.Constant(null, type);
                }
                else if (type.IsEnum)
                {
                    return Expression.Constant(val.TryParse(type), type);
                }
                else if (type.IsSimple() && val.GetType() != type)
                {
                    return Expression.Constant(Convert.ChangeType(val, type), type);
                }
                else
                {
                    return Expression.Constant(val, type);
                }
            }
            throw new InvalidOperationException("Cannot extract constant value.");
        }

    }
}
