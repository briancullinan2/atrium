using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace DataLayer.Utilities
{

    public class RootReplacementVisitor(IQueryable realRoot) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            // If we hit the 'EnqueuedQueryable' at the bottom of the tree, 
            // replace it with the actual DbSet.Expression
            if (node.Value is IQueryable queryable && queryable.Provider.GetType().GetGenericTypeDefinition() == typeof(EnqueuedQueryProvider<>))
            {
                return realRoot.Expression;
            }
            return base.VisitConstant(node);
        }
    }



    public class ParameterUpdateVisitor(ParameterExpression oldParam, Expression newExpression) : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParam = oldParam;
        private readonly Expression _newExpression = newExpression;

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _oldParam) return _newExpression;
            return base.VisitParameter(node);
        }
    }


    public class ClosureEvaluatorVisitor : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
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
    }

}
