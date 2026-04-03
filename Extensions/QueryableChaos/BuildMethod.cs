
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {


        private static Expression BuildMethodCall(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Resolve the actual MethodInfo (Where, Select, etc.)
            var methodEl = el.Element(nameof(MethodCallExpression.Method))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing Method metadata");

            var methodInfo = ResolveMetadata(typeof(MethodInfo), null, methodEl) as MethodInfo
                ?? throw new InvalidOperationException("MethodInfo resolution failed");

            // 2. Resolve the Instance (The 'Object' attribute in your XML)
            // For LINQ extensions (static), this is usually null.
            var instanceEl = el.Element(nameof(MethodCallExpression.Object))?.Elements().FirstOrDefault();
            var instance = instanceEl != null ? ToExpression(instanceEl) : null;

            // 3. Resolve Arguments
            var args = el.Element(nameof(MethodCallExpression.Arguments))?.Elements()
                .Select(x =>
                {
                    var expr = ToExpression(x)!;

                    //if (expr is LambdaExpression)
                    //{
                    // EF Core needs the 'Quote' to treat the lambda as data, not code
                    //    return Expression.Quote(expr);
                    //}
                    // UNWRAP QUOTES: If the node is a Quote, we often need the 
                    // underlying Lambda for the method call to validate types correctly.
                    if (expr is UnaryExpression unary && expr.NodeType == ExpressionType.Quote)
                    {
                        return unary.Operand;
                    }

                    return expr;
                })
                .ToList() ?? [];

            // 4. Use the static factory directly - much cleaner than a factoryMap
            return Expression.Call(instance, methodInfo, args);
        }




        private static InvocationExpression BuildInvocation(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // The 'Expression' being invoked (usually a Lambda)
            var expressionEl = el.Element("Expression")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Invoke missing target expression");
            var target = ToExpression(expressionEl)!;

            // The arguments being passed to that lambda
            var args = el.Element("Arguments")?.Elements()
                .Select(x => ToExpression(x)!)
                .ToList() ?? [];

            return Expression.Invoke(target, args);
        }


        /*
        public static Expression? RebuildInvoker(XElement el, Func<XElement, Expression?> ToExpression)
        {
            string nodeType = el.Attribute("NodeType")?.Value ?? "";

            return nodeType switch
            {
                "Call" => BuildMethodCall(el, ToExpression),
                "Invoke" => BuildInvocation(el, ToExpression),
                "Lambda" => BuildLambda(el, ToExpression),
                "Constant" => BuildConstant(el, ToExpression), // etc...
                _ => throw new NotSupportedException($"NodeType {nodeType} not implemented.")
            };
        }
        */


        private static Expression? BuildLambda(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var parametersEl = el.Element(nameof(LambdaExpression.Parameters))?.Elements();
            var bodyEl = el.Element(nameof(LambdaExpression.Body))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing body");

            var parameters = parametersEl?.Select(p => ToExpression(p)).Cast<ParameterExpression>().ToList() ?? [];
            var realBody = ToExpression(bodyEl)
                ?? throw new InvalidOperationException("Body resolution failed");

            // 1. Get the intended Delegate Type (e.g., Func<Role, IEnumerable<Group>>)
            var typeName = el.Element(nameof(LambdaExpression.Type))?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                          ?? el.Element(nameof(LambdaExpression.Type))?.Value;

            if (!string.IsNullOrEmpty(typeName))
            {
                var delegateType = Type.GetType(typeName);
                if (delegateType != null)
                {
                    // 2. Force the lambda to implement this specific delegate
                    return Expression.Lambda(delegateType, realBody, parameters);
                }
            }

            // Fallback to inference if Type metadata is missing
            return Expression.Lambda(realBody, parameters);
        }





    }
}
