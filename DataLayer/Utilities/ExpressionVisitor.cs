using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace DataLayer.Utilities
{

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
