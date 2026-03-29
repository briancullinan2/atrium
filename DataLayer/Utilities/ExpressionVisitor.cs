using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using static DataLayer.Utilities.AggressiveVisitor;

namespace DataLayer.Utilities
{
    

    public class RootReplacementVisitor(IQueryable? realRoot) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (realRoot != null
                && node.Value is IQueryable queryable
                && (queryable.Provider.GetType().Extends(typeof(EnqueuedQueryProvider<>))
                || node.Type.Extends(typeof(AsyncQueryable<>))))
            {
                return realRoot.Expression;
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node.Type.Extends(typeof(IQueryable<>)))
            {
                realRoot ??= ((IEnumerable)Activator.CreateInstance(typeof(List<>)
                    .MakeGenericType(node.Type.GenericTypeArguments))!).AsQueryable();
                return realRoot.Expression;
            }

            if (node.Type.Extends(typeof(Entities.Entity<>)))
            {
                realRoot ??= ((IEnumerable)Activator.CreateInstance(typeof(List<>)
                    .MakeGenericType(node.Type))!).AsQueryable();
                return realRoot.Expression;
            }

            if (node.CanReduce)
            {
                return Visit(node.Reduce())!;
            }

            return base.VisitExtension(node);
        }


        protected override Expression VisitParameter(ParameterExpression node)
        {
            /*
            if (node.Type.Extends(typeof(Entities.Entity<>)))
                realRoot ??= ((IEnumerable)Activator.CreateInstance(typeof(List<>)
                    .MakeGenericType(node.Type))!).AsQueryable();
            if(node.Type.Extends(typeof(IQueryable<>)))
                realRoot ??= ((IEnumerable)Activator.CreateInstance(typeof(List<>)
                    .MakeGenericType(node.Type.GenericTypeArguments))!).AsQueryable();
            */

            if (node == CurrentRecording?.Parameter
                && CurrentRecording?.NewParameter != null)
                return CurrentRecording.NewParameter;
            return base.VisitParameter(node);
        }


        public class ClosureRecording
        {
            [JsonIgnore]
            public Type? MemberAccess { get; set; }
            public string? MemberAccessName { get => MemberAccess?.AssemblyQualifiedName; }

            [JsonIgnore]
            public Type? EntityType { get; set; }
            public string? EntityTypeName { get => EntityType?.AssemblyQualifiedName; }

            [JsonIgnore]
            public ParameterExpression? Parameter { get; internal set; }
            public string? ParameterName { get => Parameter?.Name; }

            [JsonIgnore]
            public Expression? NewParameter { get; internal set; }
        }


        public ClosureRecording? CurrentRecording { get; set; } = null;
        public Expression? Root { get; set; }


        private int _depth = 0;

        public override Expression? Visit(Expression? node)
        {
            _depth++;
            try
            {
                if (_depth == 1)
                {
                    Root = node;
                }

                return base.Visit(node);
            }
            finally
            {
                _depth--;
            }
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (node is LambdaExpression lambda)
            {
                if (Root == node
                    && lambda.Body is MethodCallExpression methodCall
                    && lambda.Parameters.FirstOrDefault() is ParameterExpression set
                    && (set.Type.Extends(typeof(DbSet<>))
                    || set.Type.Extends(typeof(IQueryable<>))))
                {
                    CurrentRecording = new ClosureRecording();
                    CurrentRecording?.Parameter = set;
                    CurrentRecording?.NewParameter = realRoot?.Expression;
                    return VisitMethodCall(methodCall);
                }
            }


            return base.VisitLambda(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // 1. Only process Queryable methods (Where, Select, SelectMany, etc.)
            if (node.Method.DeclaringType != typeof(Queryable))
                return base.VisitMethodCall(node);

            var genericArgs = node.Method.GetGenericArguments().FirstOrDefault();
            var quoted = node.Arguments.FirstOrDefault(a => a.Type.Extends(typeof(Expression)));
            var lamda = quoted is UnaryExpression quote ? quote.Operand as LambdaExpression : quoted as LambdaExpression;
            var predicateArg = lamda?.Parameters.FirstOrDefault();

            var possiblyEntity = predicateArg?.Type.GetGenericArguments()
                .FirstOrDefault()?.GetGenericArguments().FirstOrDefault()
                ?? genericArgs;

            if (predicateArg != null && possiblyEntity != null
            //    && genericArgs == typeof(object)
                && genericArgs != possiblyEntity
            )
            {


                CurrentRecording ??= new ClosureRecording();
                CurrentRecording?.MemberAccess = possiblyEntity;
                if (possiblyEntity.Extends(typeof(Entities.Entity<>)) == true)
                {
                    CurrentRecording?.EntityType = possiblyEntity;
                }

                // only replace parameter is type doesn't match or it's the root
                //if (genericArgs != possiblyEntity)
                {
                    var newParameter = Expression.Parameter(predicateArg.Type.GetGenericTypeDefinition()
                        .MakeGenericType(possiblyEntity), predicateArg.Name);

                    CurrentRecording?.Parameter = node.Arguments.First() as ParameterExpression;
                    CurrentRecording?.NewParameter = newParameter;
                }

                var newMethod = typeof(Queryable).GetMethods(node.Method.Name)
                    .FirstOrDefault(m => m.GetParameters().Length == node.Method.GetParameters().Length)
                    ?.MakeGenericMethod(possiblyEntity)
                    ?? throw new InvalidOperationException("Could not render new Expression method");

                var arguments = node.Arguments.Select(Visit).Cast<Expression>().ToList();

                return Expression.Call(newMethod, arguments);
            }

            return base.VisitMethodCall(node);
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
