
using System.Xml.Linq;


namespace Extensions.QueryableChaos;

public static partial class LinqExtensions
{
    

    // A registry to map NodeType to the correct Factory Method
    private static readonly Dictionary<string, ExpressionType> _nodeTypeLookup = Enum.GetValues<ExpressionType>()
        .Cast<ExpressionType>()
        .ToDictionary(t => t.ToString(), t => t);
    private static readonly Dictionary<ExpressionType, List<Tuple<MethodInfo, List<ParameterInfo>>>> _factoryMap = typeof(Expression)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        // We filter for methods that take Expression arguments to avoid overloads like 'Constant'
        .Where(m => _nodeTypeLookup.ContainsKey(m.Name))
        .GroupBy(m => _nodeTypeLookup[m.Name])
        .ToDictionary(g => g.Key, g => g.Select(g => new Tuple<MethodInfo, List<ParameterInfo>>(g, [.. g.GetParameters()])).ToList());
    private static readonly Dictionary<ExpressionType, ExpressionFactory> _elementMap =
        new() {
            { ExpressionType.Call, BuildMethodCall },
            { ExpressionType.Parameter, BuildParameter },
            { ExpressionType.Constant, BuildConstant },
            { ExpressionType.Quote, BuildUnary },
            { ExpressionType.Lambda, BuildLambda },
            { ExpressionType.Convert, BuildUnary },
            { ExpressionType.MemberAccess, BuildProperty },
            { ExpressionType.Index, BuildIndex },
            { ExpressionType.Invoke, BuildInvocation },
            { ExpressionType.Conditional, BuildConditional },
            { ExpressionType.NewArrayInit, BuildNewArrayInit },

            // Binary Expressions
            { ExpressionType.Not, BuildUnary },
            { ExpressionType.OrElse, BuildLeftRight },
            { ExpressionType.AndAlso, BuildLeftRight },

            // Comparators
            { ExpressionType.Equal, BuildLeftRight },
            { ExpressionType.NotEqual, BuildLeftRight },
            { ExpressionType.LessThan, BuildLeftRight },
            { ExpressionType.LessThanOrEqual, BuildLeftRight },
            { ExpressionType.GreaterThan, BuildLeftRight },
            { ExpressionType.GreaterThanOrEqual, BuildLeftRight },
            
            // Arithmetic
            { ExpressionType.Add, BuildLeftRight },
            { ExpressionType.Subtract, BuildLeftRight },
            { ExpressionType.Multiply, BuildLeftRight },
            { ExpressionType.Divide, BuildLeftRight },
            { ExpressionType.Modulo, BuildLeftRight },
            { ExpressionType.Power, BuildLeftRight },

            // Bitwise
            { ExpressionType.And, BuildLeftRight },
            { ExpressionType.Or, BuildLeftRight },
            { ExpressionType.ExclusiveOr, BuildLeftRight },

            // Null Handling (Essential for your 'Atrium' entities)
            { ExpressionType.Coalesce, BuildLeftRight },
            { ExpressionType.TypeAs, BuildTypeTest },
            { ExpressionType.TypeIs, BuildTypeTest },
            { ExpressionType.TypeEqual, BuildTypeTest },

            // Comparisons (You already have most, but don't forget these)
            //{ ExpressionType.TypeIs, BuildLeftRight } // Used for "is" keyword checks


            //{ExpressionType.Extension, BuildExtension}
        };

    public delegate Expression? ExpressionFactory(XElement el, Func<XElement, Expression?> ToExpression);



    private static Tuple<Expression?, IQueryable?> DumbToExpressionOutWrapper(XElement el, ITranslationContext context)
    {
        var result = ToExpression(el, context, out var outish);
        return new Tuple<Expression?, IQueryable?>(result, outish);
    }

    public static Expression? ToExpression(this XElement el, ITranslationContext context, out IQueryable? set)
    {
        set = null;
        var typeStr = el.Attribute(nameof(Expression.NodeType))?.Value;
        if (typeStr == null || !_nodeTypeLookup.TryGetValue(typeStr, out var nodeType))
            return null;

        // Special cases: Parameters and Constants usually need manual handling 
        // because they don't follow the "Children as Expressions" rule perfectly.
        if (nodeType == ExpressionType.Extension)
        {
            var typeName = el.Element(nameof(IQueryable.ElementType))?
                .Attribute(nameof(Type.AssemblyQualifiedName))?.Value;
            if (typeName?.Contains(nameof(Extensions)) == true)
            {
                return BuildExtension(el, context, out set); // Swap placeholder for Real DB source
            }
        }
        IQueryable? outish = null;
        if (_elementMap.TryGetValue(nodeType, out var factoryMethod))
        {
            var result = factoryMethod.Invoke(el, el2 =>
            {
                var result = DumbToExpressionOutWrapper(el2, context);
                if (result.Item2 != null) outish = result.Item2;
                return result.Item1 as Expression;
            }) as Expression;
            if (outish != null) set = outish;
            return result;
        }
        if (el.Attribute(nameof(Type.AssemblyQualifiedName)) is XAttribute typeAttr
            && Type.GetType(typeAttr.Value) is Type targetType)
        {
            return Expression.Constant(ResolveMetadata(targetType, el.ToString(), el));
        }

        throw new NotSupportedException($"No factory found for {nodeType}");
    }



}
