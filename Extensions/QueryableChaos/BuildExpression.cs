using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {


        private static Expression BuildNewArrayInit(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Get the array type (e.g., System.Object[])
            var typeName = el.Element(nameof(NewArrayExpression.Type))?
                .Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                ?? el.Element(nameof(NewArrayExpression.Type))?.Value;
            var arrayType = Type.GetType(typeName!)
                            ?? throw new InvalidOperationException($"Array type not found: {typeName}");

            // 2. Get the element type (System.Object)
            var elementType = arrayType.GetElementType() ?? typeof(object);

            // 3. Resolve all the expressions inside the { ... }
            var expressions = el.Element(nameof(NewArrayExpression.Expressions))?.Elements()
                .Select(x => ToExpression(x)!)
                .ToList() ?? [];

            return Expression.NewArrayInit(elementType, expressions);
        }

        private static Expression BuildConditional(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var test = ToExpression(el.Element(nameof(ConditionalExpression.Test))?.Elements().First()!)!;
            var ifTrue = ToExpression(el.Element(nameof(ConditionalExpression.IfTrue))?.Elements().First()!)!;
            var ifFalse = ToExpression(el.Element(nameof(ConditionalExpression.IfFalse))?.Elements().First()!)!;

            return Expression.Condition(test, ifTrue, ifFalse);
        }



        private static Expression? BuildTypeTest(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var rightEl = (el.Element(nameof(TypeBinaryExpression.TypeOperand)) ?? el.Element(nameof(UnaryExpression.Type)))?.Elements().FirstOrDefault();
            var leftEl = (el.Element(nameof(TypeBinaryExpression.Expression)) ?? el.Element(nameof(UnaryExpression.Operand)))?.Elements().FirstOrDefault();
            if (rightEl == null || leftEl == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }
            var rightOperand = ResolveMetadata(typeof(Type), rightEl.ToString(), rightEl) as Type;
            var leftOperand = ToExpression(leftEl);
            if (rightOperand == null || leftOperand == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }

            var nodeType = el.Attribute(nameof(TypeBinaryExpression.NodeType))?.Value;

            return nodeType switch
            {
                nameof(ExpressionType.TypeIs) => Expression.TypeIs(leftOperand, rightOperand),
                nameof(ExpressionType.TypeAs) => Expression.TypeAs(leftOperand, rightOperand),
                nameof(ExpressionType.TypeEqual) => Expression.TypeEqual(leftOperand, rightOperand),
                _ => throw new InvalidOperationException($"Node Type '{nodeType}' not supported on host.")
            };

        }

        private static Expression? BuildUnary(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var operandEl = el.Element(nameof(UnaryExpression.Operand))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException($"Missing operand: {el}");

            var operand = ToExpression(operandEl)
                ?? throw new InvalidOperationException("Operand resolution failed.");

            var nodeType = el.Attribute(nameof(UnaryExpression.NodeType))?.Value;

            if (nodeType == nameof(ExpressionType.Not)) return Expression.Not(operand);

            if (nodeType == nameof(ExpressionType.Quote)) return Expression.Quote(operand);

            if (nodeType == nameof(ExpressionType.Convert))
            {
                // 1. Prioritize AssemblyQualifiedName for Generics/Nullables
                var typeEl = el.Element(nameof(UnaryExpression.Type));
                var typeName = typeEl?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                              ?? typeEl?.Attribute(nameof(Type.FullName))?.Value
                              ?? throw new InvalidOperationException("Missing type metadata.");

                var resolvedType = Type.GetType(typeName);

                // 2. Fallback for common types if reflection is being finicky
                if (resolvedType == null && typeName.Contains(nameof(Nullable)))
                {
                    // Handle manual assembly loading or specific mappings if needed
                    throw new InvalidOperationException($"Type load failed for: {typeName}");
                }

                return Expression.Convert(operand, resolvedType!);
            }

            throw new NotSupportedException($"Unary NodeType {nodeType} not implemented.");
        }




    }
}
