using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

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

    public class ExpressionMinifier : ExpressionVisitor
    {
        private readonly XElement _root = new("Exp");
        private XElement _current;

        public ExpressionMinifier() => _current = _root;

        public static string Minify(Expression node)
        {
            var visitor = new ExpressionMinifier();
            visitor.Visit(node);
            return visitor._root.ToString(SaveOptions.DisableFormatting);
        }

        public override Expression? Visit(Expression? node)
        {
            if (node == null) return null;

            // Truncate "MemberExpression" to "Mem", "ConstantExpression" to "Con", etc.
            var nodeName = node.GetType().Name[..3];
            var element = new XElement(nodeName);

            var parent = _current;
            _current = element;
            parent.Add(element);

            base.Visit(node);

            _current = parent;
            return node;
        }
    }


    public class XNodeTruncator
    {
        public static XDocument Truncate(XDocument doc) =>
            new(doc.Declaration, Truncate(doc.Root!));

        private static XElement Truncate(XElement el)
        {
            try
            {
                return new XElement(el.Name.LocalName[..Math.Min(3, el.Name.LocalName.Length)],
                    el.Attributes().Select(a => new XAttribute(
                        a.Name.LocalName[..Math.Min(3, a.Name.LocalName.Length)],
                        a.Value?[..Math.Min(3, a.Value?.Length ?? 0)] ?? string.Empty)).DistinctBy(attr => attr.Name),
                    el.Elements().Select(Truncate),
                    el.HasElements ? null : el.Value?[..Math.Min(3, el.Value?.Length ?? 0)] ?? string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
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

}
