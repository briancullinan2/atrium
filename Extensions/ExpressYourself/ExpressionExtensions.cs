using Extensions.Entities;
using Extensions.PrometheusTypes;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Extensions.PrometheusTypes
{
    public static partial class ExpressionExtensions
    {
        

        private static Type? GetEnumerableElementType(Type type)
        {
            if (type == typeof(string)) return null;
            if (type.IsArray) return type.GetElementType();
            return type.GetInterfaces()
                       .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                       ?.GetGenericArguments().FirstOrDefault();
        }




        private static void ParseExpression(Expression? expr, Dictionary<MemberInfo, object?> values)
        {
            if (expr == null) return;

            // 1. Handle Ternary logic branches
            if (expr is LambdaExpression le)
            {
                // Dive into the body of the lambda (e.g., s => [Body])
                ParseExpression(le.Body, values);
            }
            else if (expr is ConditionalExpression ce)
            {
                ParseExpression(ce.Test, values);
                ParseExpression(ce.IfTrue, values);
                ParseExpression(ce.IfFalse, values);
            }
            // 2. Handle Binary logic (And, Or, and Comparisons)
            else if (expr is BinaryExpression binary)
            {
                switch (binary.NodeType)
                {
                    case ExpressionType.AndAlso:
                    case ExpressionType.OrElse:
                        // Logical connectors: recurse both sides
                        ParseExpression(binary.Left, values);
                        ParseExpression(binary.Right, values);
                        break;

                    case ExpressionType.Equal:
                    case ExpressionType.NotEqual:
                    case ExpressionType.GreaterThan:
                    case ExpressionType.LessThan:
                    case ExpressionType.GreaterThanOrEqual:
                    case ExpressionType.LessThanOrEqual:
                        // Comparison: Identify Member vs Value
                        ExtractComparison(binary, values);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown comparison: " + binary.NodeType);
                }
            }
            // 3. Handle Method calls (like .ToString())
            else if (expr is MethodCallExpression mc)
            {
                if (mc.Object is MemberExpression me)
                {
                    values[me.Member] = ResolveValue(mc);
                }

                foreach (var arg in mc.Arguments)
                {
                    Merge(values, arg.ToMembers());
                }
            }
            else if (expr is NewExpression ne) Merge(values, ne.ToMembers());
            else if (expr is MemberInitExpression mi) Merge(values, mi.ToMembers());
            else if (expr is ParameterExpression pe)
            {
                // This is 's' in 's => s.Name'
                // We don't store a 'value' because it's the iterator, 
                // but we capture the Type to differentiate a Query<Visit> from a Query<User>.
                values[pe.Type.GetTypeInfo()] = pe.Name;
            }
            else if (expr is UnaryExpression ue)
            {
                // (object)x or (int?)y - just peel the onion and keep going
                ParseExpression(ue.Operand, values);
            }
            else if (expr is ConstantExpression co
                && co.Value?.GetType().Extends(typeof(AsyncQueryable<>)) == true)
            {

            }
            else
            {
                throw new InvalidOperationException("Can't do anything else with this " + expr.GetType() + ", frankly.");
            }

        }

        private static void Merge(Dictionary<MemberInfo, object?> target, Dictionary<MemberInfo, object?> source)
        {
            foreach (var kvp in source)
            {
                target[kvp.Key] = kvp.Value;
            }
        }


        private static Expression Unbox(Expression expr)
        {
            // Strips away (object), Nullable conversion, or other Unary wrappers
            while (expr is UnaryExpression unary)
            {
                expr = unary.Operand;
            }
            return expr;
        }


        private static object? ResolveValue(Expression expr)
        {
            if (expr is ConstantExpression constant)
            {
                return constant.Value;
            }

            // This is the "Wiggle Room" for the Ternary
            if (expr is ConditionalExpression ce)
            {
                // We have to evaluate the Test to know which branch to take
                var testFunc = Expression.Lambda<Func<bool>>(ce.Test).Compile();
                return testFunc() ? ResolveValue(ce.IfTrue) : ResolveValue(ce.IfFalse);
            }

            // Fallback for closures/variables
            try
            {
                return Expression.Lambda(expr).Compile().DynamicInvoke();
            }
            catch
            {
                return null;
            }
        }



        public static MethodCallExpression Identity(this Type entityType)
        {
            // 1. Create a concrete, empty List of the specific runtime type
            // This is equivalent to 'new List<User>()'
            if (entityType.Extends(typeof(IEnumerable<>)))
                entityType = entityType.GenericTypeArguments.FirstOrDefault()
                    ?? throw new InvalidOperationException("Can't determine entity type.");


            var listType = typeof(List<>).MakeGenericType(entityType);
            var emptyList = Activator.CreateInstance(listType);
            var listConstant = Expression.Constant(emptyList, listType);

            var asQueryableMethod = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethod)
                .MakeGenericMethod(entityType);

            var iqueryableSource = Expression.Call(null, asQueryableMethod, listConstant);

            // 3. NOW call ToListAsync using the wrapped source
            var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.IsGenericMethod)
                .MakeGenericMethod(entityType);

            // arg0 is now IQueryable<T>, arg1 is CancellationToken
            var finalCall = Expression.Call(
                null,
                toListAsyncMethod,
                iqueryableSource,
                Expression.Constant(default(CancellationToken))
            );

            return finalCall;
        }




        [GeneratedRegex(@"(?<name>[^\[]+)(?:\[(?<index>\d+)\])?")]
        private static partial Regex BasicIndexerParser();


    }
}
