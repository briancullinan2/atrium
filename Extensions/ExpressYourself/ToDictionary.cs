
namespace Extensions.PrometheusTypes
{
    public static partial class ExpressionExtensions
    {


        public static Dictionary<string, string?> ToDictionary(this Expression expression)
        {
            var cleanExpression = new ClosureEvaluatorVisitor().Visit(expression);

            return cleanExpression.ToMembers().ToDictionary(dkv => dkv.Key.Name, dkv => dkv.Value?.ToString());
        }



    }
}
