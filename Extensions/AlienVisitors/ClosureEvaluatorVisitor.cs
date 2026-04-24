
namespace Extensions.AlienVisitors;



public class ClosureEvaluatorVisitor : ExpressionVisitor
{


    protected override Expression VisitMember(MemberExpression node)
    {
        if (TryEvaluate(node) is Expression expr) return expr;

        if (node.IsClosure())
        {
            // Evaluate the member access chain into a real value
            

            // Replace the complex 'u.Username.Equals(value(DisplayClass).claim.Value)' 
            // with 'u.Username.Equals("Brian")'
            return Expression.Constant(node.Evaluate(), node.Type);
        }

        return base.VisitMember(node);
    }



    protected override Expression VisitIndex(IndexExpression node) =>
        TryEvaluate(node) ?? base.VisitIndex(node);

    private static ConstantExpression? TryEvaluate(Expression node)
    {
        var root = GetRoot(node);
        // If it's a constant (DisplayClass, ValueBuffer, or just a local variable)
        if (root is ConstantExpression && !typeof(IQueryable).IsAssignableFrom(root.Type))
        {
            var getter = Expression.Lambda(node).Compile();
            return Expression.Constant(getter.DynamicInvoke(), node.Type);
        }
        return null;
    }

    private static Expression GetRoot(Expression node)
    {
        while (node is MemberExpression m) node = m.Expression!;
        while (node is IndexExpression i) node = i.Object!;
        return node;
    }
}
