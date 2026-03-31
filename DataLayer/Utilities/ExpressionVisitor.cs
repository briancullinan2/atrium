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
    
    // run up the chain looking for types to replace because EF generates a bunch of plain ol objects for entities
    //   second replace the parts we found in the first pass with the correct entity type
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

            if (node == CurrentRecording?.Parameter
                && CurrentRecording?.NewParameter != null)
                return CurrentRecording.NewParameter;
            return base.VisitParameter(node);
        }


        public class ClosureRecording : EntityRecording
        {
            [JsonIgnore]
            public Type? MemberAccess { get; set; }
            public string? MemberAccessName => MemberAccess?.AssemblyQualifiedName;

            [JsonIgnore]
            public ParameterExpression? Parameter { get; internal set; }
            public string? ParameterName => Parameter?.Name;

            [JsonIgnore]
            public Expression? NewParameter { get; internal set; }
        }


        public class EntityRecording
        {
            [JsonIgnore]
            public Type? EntityType { get; set; }
            public string? EntityTypeName => EntityType?.AssemblyQualifiedName;
        }


        public EntityRecording? CurrentEntity { get; set; } = null;
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
            
            
            
                if(lambda.Parameters.Any(p => p == CurrentRecording?.Parameter))
                {
                    // have to a rewrite
                    var parameters = lambda.Parameters.Select(Visit).Cast<ParameterExpression>().ToArray();
                    var body = Visit(lambda.Body);
                    return Expression.Lambda(body!, parameters);
                }
            }


            return base.VisitLambda(node);
        }


        protected static bool IsQueryableMethod(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(Queryable)
            || node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
            || node.Method.DeclaringType == typeof(AsyncQueryable<>);


        // UNRELIABLE this is why we need two passes?
        protected static ParameterExpression? GetQueryablePredicate(MethodCallExpression node)
        {
            var quoted = node.Arguments.FirstOrDefault(a => a.Type.Extends(typeof(Expression)));
            var lamda = quoted is UnaryExpression quote ? quote.Operand as LambdaExpression : quoted as LambdaExpression;
            return lamda?.Parameters.FirstOrDefault();
        }



        protected static Type? GetEntityType(Type type) =>
            type.Extends(typeof(Entities.Entity<>)) == true
            || type.Extends(typeof(IQueryable<>)) == true
            || type.Extends(typeof(DbSet<>)) == true 
            ? type.GenericTypeArguments[0] : null;



        // the entity type from the IQueryable set doesn't match the parameter type on the predicate
        protected static Type? MethodQueryableParameterObjectScenario(MethodCallExpression node)
        {
            // safety
            if (!IsQueryableMethod(node))
                return null;

            var enumerable = node.Arguments.FirstOrDefault()?.Type;
            var genericArgs = enumerable?.GetGenericArguments().FirstOrDefault()
                ?? node.Method.GetGenericArguments().FirstOrDefault();
            var predicateArg = GetQueryablePredicate(node);
            var possiblyEntity = predicateArg?.Type;
            // only replace parameter if type doesn't match or it's the root
            if (predicateArg != null && possiblyEntity != null
                && genericArgs != possiblyEntity)
                return genericArgs ?? possiblyEntity;
            
            return null;
        }

        protected static Type? MethodReturnsQueryable(MethodCallExpression node)
        {
            // safety
            if (!IsQueryableMethod(node))
                return null;

            if(node.Type.Extends(typeof(IQueryable)))
            {
                return node.Type.GetGenericArguments().FirstOrDefault();
            }
            return null;
        }


        protected MethodCallExpression FixMethodParameterType(MethodCallExpression node, Type shouldBe)
        {
            // 1. Get the generic method definition (e.g., Where<>)
            var methodDef = node.Method.IsGenericMethod
                ? node.Method.GetGenericMethodDefinition()
                : node.Method;

            var generics = node.Method.GetGenericArguments();
            var methodParams = methodDef.GetParameters();

            // 2. Find the argument that is a Lambda/Expression
            // We look for the parameter index that matches your predicateArg
            int predicateParamIndex = -1;
            for (int i = 0; i < methodParams.Length; i++)
            {
                if (methodParams[i].ParameterType.Extends(typeof(Expression<>)))
                {
                    predicateParamIndex = i;
                    break;
                }
            }

            if (predicateParamIndex != -1)
            {
                // 3. Identify WHICH generic argument T corresponds to that parameter
                // Most Queryable methods use genericArgs[0] as TSource
                // But we can be precise by checking the Name or Position
                var paramType = methodParams[predicateParamIndex].ParameterType; // e.g. Expression<Func<TSource, bool>>
                var genericInLambda = paramType.GetGenericArguments()[0] // Func<TSource, bool>
                                               .GetGenericArguments()[0]; // TSource

                // Find the index of TSource in the method's generic arguments
                int genericIndex = node.Method.GetGenericMethodDefinition()
                                              .GetGenericArguments()
                                              .ToList()
                                              .FindIndex(g => g == genericInLambda);

                if (genericIndex != -1)
                {
                    generics[genericIndex] = shouldBe;
                }
            }

            // 4. Rebuild with the corrected generic array
            var newMethod = methodDef.MakeGenericMethod(generics);

            // Visit arguments to trigger the Parameter swap in VisitLambda
            var arguments = node.Arguments.Select(Visit).Cast<Expression>().ToList();

            return Expression.Call(newMethod, arguments);
        }


        protected Expression FixMethodToEnumerable(MethodCallExpression node, Type shouldBe)
        {
            // 1. Get the generic arguments (e.g., [Role, Group] for SelectMany)
            var generics = node.Method.GetGenericArguments();

            // Update TSource to our real entity type
            if (generics.Length > 0) generics[0] = shouldBe;

            // 2. Find the matching method in System.Linq.Enumerable
            // We match by Name and Parameter Count to handle overloads like Where(source, predicate)
            var enumerableMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == node.Method.Name && m.IsGenericMethod)
                .First(m => m.GetParameters().Length == node.Method.GetParameters().Length)
                .MakeGenericMethod(generics);

            // 3. Process the arguments
            var arguments = new List<Expression>();
            var methodParams = enumerableMethod.GetParameters();

            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];

                // IMPORTANT: Queryable uses Expression.Quote(Lambda)
                // Enumerable wants just the Lambda.
                if (arg is UnaryExpression quote && quote.NodeType == ExpressionType.Quote)
                {
                    // Visit the Lambda to swap parameters/members, then strip the Quote
                    arguments.Add(Visit(quote.Operand));
                }
                else
                {
                    arguments.Add(Visit(arg));
                }
            }

            // 4. Create the call to the synchronous Enumerable method
            return Expression.Call(enumerableMethod, arguments);
        }


        protected MethodCallExpression FixMethodReturnType(MethodCallExpression node, Type shouldBe)
        {
            // 1. Get the generic arguments (e.g., [Role, Group] for SelectMany)
            var generics = node.Method.GetGenericArguments();

            // Update the TSource (usually the first generic) to our real entity type
            if (generics.Length > 0) generics[0] = shouldBe;

            // 2. Find the equivalent method in System.Linq.Enumerable
            // This is the key: we stop using Queryable and start using Enumerable
            var enumerableMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == node.Method.Name && m.IsGenericMethod)
                .First(m => m.GetParameters().Length == node.Method.GetParameters().Length)
                .MakeGenericMethod(generics);

            // 3. Visit the arguments
            var arguments = new List<Expression>();
            var methodParams = enumerableMethod.GetParameters();

            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];
                var targetParamType = methodParams[i].ParameterType;

                // IMPORTANT: Enumerable methods take Func<T>, NOT Expression<Func<T>>
                // If the original was an Expression (Quote), we need to unwrap it
                if (arg is UnaryExpression quote && quote.NodeType == ExpressionType.Quote)
                {
                    // Visit the inner Lambda to swap parameters, then return the Lambda itself
                    // Enumerable.Where(source, func) needs the Lambda, not the Quote
                    arguments.Add(Visit(quote.Operand));
                }
                else
                {
                    arguments.Add(Visit(arg));
                }
            }

            // 4. Return the new Call
            // This now returns a MethodCall that results in IEnumerable<T>
            return Expression.Call(enumerableMethod, arguments);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            
            if (!IsQueryableMethod(node)) // let the cleaner catch it
                return base.VisitMethodCall(node);


            // TODO: there's no way to pass the entity type UP the call chain while simultaneously replacing the
            // parameter type, so instead of doing it in one pass, do it in two, first pass is to find the entity
            // type and parameter, second pass is to replace the parameter type and member access types with
            // the correct entity type
            MethodCallExpression fixedExpression;
            if(MethodQueryableParameterObjectScenario(node) is Type set
                && GetQueryablePredicate(node) is ParameterExpression predicateArg)
            {
                CurrentEntity ??= new EntityRecording();
                CurrentEntity.EntityType = set;

                CurrentRecording ??= new ClosureRecording();
                CurrentRecording?.MemberAccess = CurrentEntity.EntityType;
                if (CurrentEntity.EntityType.Extends(typeof(Entities.Entity<>)) == true)
                {
                    CurrentRecording?.EntityType = CurrentEntity.EntityType;
                }

                var newParameter = Expression.Parameter(CurrentEntity.EntityType, predicateArg.Name);

                CurrentRecording?.Parameter = predicateArg;
                CurrentRecording?.NewParameter = newParameter;


                // TODO: fix this last one so we never end up back here on the second pass
                fixedExpression = FixMethodParameterType(node, set);
            }
            //else if (MethodReturnsQueryable(node) is Type set2)
            //{
            //    fixedExpression = FixMethodReturnType(node, set2);
            //}
            else // if(CurrentEntity != null)
                fixedExpression = (MethodCallExpression)base.VisitMethodCall(node); // UNFIXED



            // only start this from the top node, pass corrected parameter types back up call chain
            if (Root != node || CurrentEntity == null)
                return fixedExpression;


            // TODO: this works not, but now i have to go through the tree and grab the very first parameter
            //   to check if the expression starts with a queryable, need to refactor so it does the same
            //   if statement check, before processing the rest of the method call, grab the DbSet or IQueryable
            //   from the start this way i can check subsequent types like .Where().Select().First() the
            //   first or select could still be typeof(object)

            if (CurrentEntity == null)
            {
            }


            // only start this from the top node, an entity IQueryable was found at the bottom of the tree
            if (CurrentEntity != null && CurrentEntity.EntityType != null)
            {
                // signal to the vistor that parameter replacement can take place
                CurrentRecording ??= new ClosureRecording();
                CurrentRecording?.MemberAccess = CurrentEntity.EntityType;
                if (CurrentEntity.EntityType.Extends(typeof(Entities.Entity<>)) == true)
                {
                    CurrentRecording?.EntityType = CurrentEntity.EntityType;
                }


            }

            // TODO: run back through tree again and do parameter replacements
            return base.VisitMethodCall(fixedExpression);
            
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
