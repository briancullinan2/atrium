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
            if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(AsyncQueryable<>))
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

}
