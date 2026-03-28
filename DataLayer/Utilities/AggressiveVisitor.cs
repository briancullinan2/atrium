using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace DataLayer.Utilities
{
    internal class AggressiveVisitor : ExpressionVisitor
    {
#if false
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // 1. Handle OR - Forces Multiple Database Queries
            if (node.NodeType == ExpressionType.OrElse)
            {
                // Split into two independent execution paths
                Visit(node.Left);
                var leftResults = ExecuteCurrentBag();

                ClearBag();

                Visit(node.Right);
                var rightResults = ExecuteCurrentBag();

                return CombineUnion(leftResults, rightResults);
            }

            // 2. Handle AND - Sequential Filtering
            if (node.NodeType == ExpressionType.AndAlso)
            {
                // We visit the Left first to establish the Primary Index Seek
                Visit(node.Left);

                // We visit the Right, but we tag these as 'Post-Fetch' filters
                // unless they are more restrictive/indexed than the Left.
                Visit(node.Right);
                return node;
            }

            // 3. Handle Arithmetic/Logic (The "Math on one side" rule)
            if (IsArithmetic(node.NodeType))
            {
                // If we hit u.Age + 5 >= 21, throw the "Writer's Onus" error
                throw new NotSupportedException(
                    $"Arithmetic '{node.NodeType}' must be evaluated on the Constant side. " +
                    "Please rewrite as: member [operator] (constant [math] constant).");
            }

            // 4. Standard Member/Constant Mapping
            return MapMemberToConstant(node);
        }


        private void UpdateState(ExpressionType type, object value)
        {
            switch (type)
            {
                case ExpressionType.GreaterThanOrEqual:
                    state.Lower = value;
                    break;
                case ExpressionType.LessThanOrEqual:
                    state.Upper = value;
                    break;
                case ExpressionType.Equal:
                    state.Lower = value;
                    state.Upper = value;
                    break;
            }

            // Logic check: Ensure the writer hasn't created an impossible range
            if (state.Lower != null && state.Upper != null &&
                Comparer.Default.Compare(state.Lower, state.Upper) > 0)
            {
                state.IsInconsistent = true;
                throw new InvalidOperationException("This would cause an invalid result set.");
            }
        }



        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Ensure we are looking at: Property [Operator] Constant
            if (node.Left is MemberExpression member && node.Right is ConstantExpression constant)
            {
                var val = constant.Value;

                switch (node.NodeType)
                {
                    case ExpressionType.GreaterThanOrEqual:
                        // Bridge Call: queryIndex(store, index, lower: val, upper: null)
                        return RegisterRange(member.Member.Name, lower: val);

                    case ExpressionType.LessThanOrEqual:
                        // Bridge Call: queryIndex(store, index, lower: null, upper: val)
                        return RegisterRange(member.Member.Name, upper: val);

                    case ExpressionType.Equal:
                        // Bridge Call: queryIndex(store, index, lower: val, upper: val)
                        return RegisterRange(member.Member.Name, lower: val, upper: val);

                    case ExpressionType.GreaterThan:
                    case ExpressionType.LessThan:
                    case ExpressionType.NotEqual:
                        // Under A.R.S. § 44-7007, we throw to prevent unreliable 
                        // data filtering that the writer didn't explicitly close.
                        throw new NotSupportedException(
                            $"Operator {node.NodeType} is restricted. Use inclusive bounds (>=, <=) " +
                            "to satisfy AtriumCache reliability standards.");
                }
            }

            return base.VisitBinary(node);
        }
#endif

        private Expression? FilterFluff(MethodCallExpression node)
        {
            // 1. Identify "Metadata Wrappers" that have no meaning in IndexedDB
            var fluffMethods = new[]
            {
                nameof(EntityFrameworkQueryableExtensions.AsNoTracking),
                nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution),
                nameof(EntityFrameworkQueryableExtensions.TagWith),
                nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters),
                nameof(EntityFrameworkQueryableExtensions.AsAsyncEnumerable), // Common in Blazor 2026 interop
            };

            if (fluffMethods.Contains(node.Method.Name))
            {
                // Extension methods are static; Argument[0] is the 'this' (the IQueryable)
                // We bypass the method and visit the inner source directly.
                return Visit(node.Arguments[0]);
            }
            return null;
        }


        private void FilterBad(MethodCallExpression node)
        {


            if (node.Method.DeclaringType == typeof(DbFunctionsExtensions)
                || node.Method.DeclaringType == typeof(DbFunctions))
            {
                throw new NotSupportedException(
                    $"EF.Functions (like Like, FreeText) are not supported in IDB contexts");
            }

            // 5. Block complex projections that break the 'Index' handshake
            var relationalMethods = new[] { "Join", "GroupJoin", "SelectMany", "GroupBy" };
            if (relationalMethods.Contains(node.Method.Name))
            {
                throw new NotSupportedException(
                    $"Relational operation '{node.Method.Name}' requires server-side execution.");
            }


            // 3. Security Guard: Throw on unsupported terminal or complex operations
            var unsupported = new[] {
                nameof(EntityFrameworkQueryableExtensions.Include),
                nameof(EntityFrameworkQueryableExtensions.ThenInclude),
                nameof(RelationalQueryableExtensions.FromSqlRaw),
                nameof(RelationalQueryableExtensions.AsSplitQuery)
            };
            if (node.Method.DeclaringType == typeof(RelationalQueryableExtensions)
                || unsupported.Contains(node.Method.Name)
                )
            {
                throw new NotSupportedException(
                    $"The method '{node.Method.Name}' is not supported in the AtriumCache local context");
            }


        }


        public static void FilterBadNonAsync(MethodCallExpression node)
        {
            var methodName = node.Method.Name;

            // 1. Check if it's an EF/Async extension we care about
            var asyncEquivalentName = $"{methodName}Async";
            var hasAsyncCounterpart = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(m => m.Name == asyncEquivalentName);

            if (hasAsyncCounterpart)
            {
                // This is a terminal leaf like ToList, Any, or Count. 
                // A.R.S. § 44-7007 requires we force the async path for reliability.
                throw new InvalidOperationException(
                    $"Blocking synchronous '{methodName}'. Use '{asyncEquivalentName}' to maintain " +
                    "thread reliability in the AtriumCache local context.");
            }
        }





        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if(FilterFluff(node) is Expression queryable)
            {
                return queryable;
            }

            FilterBad(node);

            FilterBadNonAsync(node);


            // 2. Identify if this method has a Predicate (Func<T, bool>)
            var predicateArg = node.Arguments.FirstOrDefault(a =>
                a.Type.IsGenericType && a.Type.GetGenericTypeDefinition() == typeof(Expression<>));

            if (predicateArg != null)
            {
                // This is where we drill into the Lambda to fill our "MethodFilters" dictionary
                // TODO: record predicates and binary
            }



            // 2. Handle 'Where' - The Core Handshake
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                || node.Method.DeclaringType == typeof(Queryable)
                || node.Method.Name == nameof(Queryable.Where))
            {
                // We keep the 'Where' but its arguments (the lambda) 
                // will be cleaned by our VisitMember override.
                return base.VisitMethodCall(node);
            }

            throw new InvalidOperationException($"Method '{node.Method.Name}' is not part of the trusted provider chain.");
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // 1. If this branch eventually leads back to the Lambda Parameter 'u',
            // we must keep it as a MemberExpression for our Whitelist/Index lookup.
            if (IsEntityRooted(node))
            {
                return base.VisitMember(node);
            }

            // 2. Otherwise, it's an external value (e.g., claim.Value, local vars).
            // We evaluate it now so the JS bridge gets a Constant.
            try
            {
                var objectMember = Expression.Convert(node, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                var value = getterLambda.Compile()();
                return Expression.Constant(value);
            }
            catch
            {
                // If evaluation fails (e.g., null ref in the closure), 
                // we fall back to standard behavior.
                return base.VisitMember(node);
            }
        }

        private static bool IsEntityRooted(Expression? node)
        {
            if (node == null) return false;

            // Follow the breadcrumbs: u.Profile.Username -> u.Profile -> u
            if (node is ParameterExpression) return true;
            if (node is MemberExpression member) return IsEntityRooted(member.Expression);
            if (node is UnaryExpression unary) return IsEntityRooted(unary.Operand);

            return false;
        }

    }
}
