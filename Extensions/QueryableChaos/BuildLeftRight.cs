using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {

        private static Expression? BuildLeftRight(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var rightEl = el.Element("Right")?.Elements().FirstOrDefault();
            var leftEl = el.Element("Left")?.Elements().FirstOrDefault();
            if (rightEl == null || leftEl == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }
            var rightOperand = ToExpression(rightEl);
            var leftOperand = ToExpression(leftEl);
            if (rightOperand == null || leftOperand == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }

            var nodeType = el.Attribute(nameof(BinaryExpression.NodeType))?.Value;

            return nodeType switch
            {
                nameof(ExpressionType.Equal) => Expression.Equal(leftOperand, rightOperand),
                nameof(ExpressionType.NotEqual) => Expression.NotEqual(leftOperand, rightOperand),
                nameof(ExpressionType.AndAlso) => Expression.AndAlso(leftOperand, rightOperand),
                nameof(ExpressionType.OrElse) => Expression.OrElse(leftOperand, rightOperand),
                nameof(ExpressionType.LessThan) => Expression.LessThan(leftOperand, rightOperand),
                nameof(ExpressionType.LessThanOrEqual) => Expression.LessThanOrEqual(leftOperand, rightOperand),
                nameof(ExpressionType.GreaterThan) => Expression.GreaterThan(leftOperand, rightOperand),
                nameof(ExpressionType.GreaterThanOrEqual) => Expression.GreaterThanOrEqual(leftOperand, rightOperand),

                // The New Arithmetic Peers
                nameof(ExpressionType.Add) => Expression.Add(leftOperand, rightOperand),
                nameof(ExpressionType.Subtract) => Expression.Subtract(leftOperand, rightOperand),
                nameof(ExpressionType.Multiply) => Expression.Multiply(leftOperand, rightOperand),
                nameof(ExpressionType.Divide) => Expression.Divide(leftOperand, rightOperand),
                nameof(ExpressionType.Modulo) => Expression.Modulo(leftOperand, rightOperand),
                nameof(ExpressionType.Power) => Expression.Power(leftOperand, rightOperand),


                // The Bitwise & Null-Safety Peers
                nameof(ExpressionType.And) => Expression.And(leftOperand, rightOperand),
                nameof(ExpressionType.Or) => Expression.Or(leftOperand, rightOperand),
                nameof(ExpressionType.Coalesce) => Expression.Coalesce(leftOperand, rightOperand),
                nameof(ExpressionType.ExclusiveOr) => Expression.ExclusiveOr(leftOperand, rightOperand),

                _ => throw new InvalidOperationException($"Node Type '{nodeType}' not supported on host.")
            };
        }


    }
}
