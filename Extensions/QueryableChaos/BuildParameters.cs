
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {

        // Keep a cache of parameters during a single Reconstruction pass
        internal static readonly Dictionary<string, ParameterExpression> _parameters = [];
        private static ParameterExpression BuildParameter(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Extract Name and Type accurately from the XML
            var name = el.Attribute(nameof(ParameterExpression.Name))?.Value ?? "x";
            var typeEl = el.Element(nameof(ParameterExpression.Type));
            var typeName = typeEl?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                          ?? typeEl?.Attribute(nameof(Type.FullName))?.Value
                          ?? throw new InvalidOperationException("Could not resolve parameter type");

            var type = Type.GetType(typeName) ?? typeof(object);

            // 2. Create a unique key using BOTH Name and Type
            var key = $"{name}_{type.FullName}";

            if (!_parameters.TryGetValue(key, out var parameter))
            {
                parameter = Expression.Parameter(type, name);
                _parameters.Add(key, parameter);
            }
            return parameter;
        }



        private static Expression? BuildExtension(XElement el, DbContext context, out IQueryable? set)
        {
            var typeName = (el.Element(nameof(IQueryable.ElementType))?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value)
                ?? throw new InvalidOperationException("Could not resolve extension type on: " + el);

            var entityType = Type.GetType(typeName)
                ?? throw new InvalidOperationException("Could not resolve type on: " + el);

            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), [])
                    ?.MakeGenericMethod(entityType)
                    ?? throw new InvalidOperationException("Could not render set creator in context: " + el);
            set = setMethod.Invoke(context, []) as IQueryable;
            return set?.Expression;
        }

    }
}
