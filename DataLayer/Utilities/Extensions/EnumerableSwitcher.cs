using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace DataLayer.Utilities.Extensions
{
    public class EnumerableSwitcher(Type entityType, Expression newRoot) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Replace the original IQueryable source with our List.AsQueryable() or List
            if (typeof(IQueryable).IsAssignableFrom(node.Type))
            {
                return newRoot;
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(Queryable) &&
                node.Method.DeclaringType?.Name != "EntityFrameworkQueryableExtensions")
                return base.VisitMethodCall(node);

            var generics = node.Method.GetGenericArguments();
            // Use your existing shouldBe logic here
            if (generics.Length > 0 && (generics[0] == typeof(object) || generics[0].Name == "Object"))
                generics[0] = entityType;

            // 1. Get all candidates with the same name
            var methodDef = typeof(Enumerable)
                .GetMethods(node.Method.Name, node.Method.GetGenericArguments().Length, 
                    [.. node.Arguments.Select<Expression, Type>(p => p.Type switch {
                        _ when p.Type.Extends(typeof(IEnumerable<>)) => typeof(IEnumerable<>),
                        _ when p.Type.Extends(typeof(Expression<>)) => typeof(Expression<>),
                        _ => typeof(object)
                    })])
                .FirstOrDefault()
                ?? typeof(Enumerable).GetMethods(node.Method.Name, node.Method.GetGenericArguments().Length,
                    [.. node.Arguments.Select<Expression, Type>(p => p.Type switch {
                        _ when p.Type.Extends(typeof(IEnumerable<>)) => typeof(IEnumerable<>),
                        _ when p.Type.Extends(typeof(Expression<>)) => typeof(Func<,>),
                        _ => typeof(object)
                    })])
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Could not find Enumerable replacement for: " + node.Method);

            var enumerableMethod = methodDef.MakeGenericMethod(generics);

            // 3. Rebuild arguments and strip Quotes
            var args = new List<Expression>();
            var targetParams = enumerableMethod.GetParameters();

            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];

                // Check if the target parameter is a Delegate (Func) but we have an Expression (Quote)
                if (typeof(Delegate).IsAssignableFrom(targetParams[i].ParameterType) &&
                    arg is UnaryExpression quote && quote.NodeType == ExpressionType.Quote)
                {
                    args.Add(Visit(quote.Operand));
                }
                else
                {
                    args.Add(Visit(arg));
                }
            }

            return Expression.Call(null, enumerableMethod, args);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // When we visit the Lambda inside a Quote, we need to ensure 
            // the parameters match our new entityType
            var parameters = node.Parameters.Select(p =>
                p.Type.Name == "Object" ? Expression.Parameter(entityType, p.Name) : p
            ).ToArray();

            // Rewrite the body using the new parameters
            var body = new ParameterUpdateVisitor(node.Parameters[0], parameters[0]).Visit(node.Body);

            return Expression.Lambda(body, parameters);
        }
    }

    // Simple helper to swap 'e' (object) for 'e' (Setting) inside the Lambda body
    internal class ParameterUpdateVisitor(ParameterExpression oldP, ParameterExpression newP) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldP ? newP : base.VisitParameter(node);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == oldP)
                return Expression.PropertyOrField(newP, node.Member.Name);
            return base.VisitMember(node);
        }
    }
}
