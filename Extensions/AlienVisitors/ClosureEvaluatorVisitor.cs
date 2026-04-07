
namespace Extensions.AlienVisitors;



public class ClosureEvaluatorVisitor : ExpressionVisitor
{


    protected override Expression VisitMember(MemberExpression node)
    {
        if (TryEvaluate(node) is Expression expr) return expr;

        if (ClosureEvaluatorVisitor.IsClosure(node))
        {
            // Evaluate the member access chain into a real value
            var objectMember = Expression.Convert(node, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();
            var value = getter();

            // Replace the complex 'u.Username.Equals(value(DisplayClass).claim.Value)' 
            // with 'u.Username.Equals("Brian")'
            return Expression.Constant(value, node.Type);
        }

        return base.VisitMember(node);
    }

    private static bool IsClosure(MemberExpression node)
    {
        var root = ClosureEvaluatorVisitor.GetRootExpression(node);
        if (root is ConstantExpression constant && constant.Value != null)
        {
            var typeName = constant.Value.GetType().Name;
            // Matches the "BS" naming convention for C# closures
            return typeName.Contains("<>c__DisplayClass") || typeName.Contains("DisplayClass");
        }
        return false;
    }

    private static Expression GetRootExpression(Expression node)
    {
        while (node is MemberExpression member)
        {
            if (member.Expression == null) return node;

            node = member.Expression!;
        }
        return node;
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
