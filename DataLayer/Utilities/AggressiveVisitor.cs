using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace DataLayer.Utilities
{
    internal class AggressiveVisitor : ExpressionVisitor
    {

        public class RecordMethod
        {
            public string? MethodName { get; set; }
            [JsonIgnore]
            public Type? MemberAccess { get; set; }
            public string? MemberAccessName { get => MemberAccess?.AssemblyQualifiedName; }
            [JsonIgnore]
            public List<(MemberInfo Member, ExpressionType Type, object? Value)> Comparators { get; set; } = [];
            public List<Tuple<string, string, object?>> SerializableComparators
            {
                get => [.. Comparators.Select(p => new Tuple<string, string, object?>(p.Member.Name, p.Type.ToString(), p.Value))];
            }
            [JsonIgnore]
            public Type? EntityType { get; set; }
            public string? EntityTypeName { get => EntityType?.AssemblyQualifiedName; }
            public bool AddBoth { get; set; } = false;
            public bool HasArithmetic { get; set; } = false;

            public RecordMethod()
            {

            }
        }

        protected RecordMethod? CurrentRecording { get; set; }
        public Dictionary<MethodInfo, RecordMethod> Recordings { get; set; } = [];


        protected override Expression VisitBinary(BinaryExpression node)
        {
            // if we reach our first comparator, start recording
            //   it doesn't matter what tree is collected, 
            //   all that matters is that there is something
            //   to compare at the top of each OrElse, with
            //   as many AndAlsos following

            if (node.NodeType == ExpressionType.Not)
            {
                // TODO: check if it can be converted
                throw new InvalidOperationException("Not operator can't be used on IDB: " + node.ToString());
            }

            if (node.IsArithmetic())
            {
                // make sure all the math is on one side and the member accessor is on the other side
                CurrentRecording?.HasArithmetic = true;
            }

            if(node.IsCompare())
            {
                if (node.NodeType == ExpressionType.GreaterThan
                    || node.NodeType == ExpressionType.LessThan)
                    throw new InvalidOperationException("Change your comparator to be inclusive >= or <=: " + node.ToString());

                // TODO: most important thing here is to initiate comparator recorder
                //   make sure member access is on one side and math and constants
                //   are on the other, then our dictionary of values will come
                //   out proper. Hack around the member accessor error we generated below
                var oldMember = CurrentRecording?.MemberAccess;
                CurrentRecording?.MemberAccess = null;
                CurrentRecording?.HasArithmetic = false;
                var left = Visit(node.Left);


                var memberIsOnLeft = false;
                var memberIsOnRight = false;
                var mathIsOnLeft = false;
                var mathIsOnRight = false;
                if (CurrentRecording?.MemberAccess != null)
                    memberIsOnLeft = true;
                if(CurrentRecording?.HasArithmetic == true)
                    mathIsOnLeft = true;


                CurrentRecording?.MemberAccess = null;
                CurrentRecording?.HasArithmetic = false;
                var right = Visit(node.Right);


                if (CurrentRecording?.MemberAccess != null)
                    memberIsOnRight = true;
                if (CurrentRecording?.HasArithmetic == true)
                    mathIsOnRight = true;


                if ((mathIsOnLeft && mathIsOnRight)
                    || (memberIsOnLeft && memberIsOnRight))
                    throw new InvalidOperationException(
                        "Expression is too complicated, put all the numbers on one side and the property access on the other.");

                CurrentRecording?.MemberAccess = oldMember;


                var newExpression = Expression.MakeBinary(node.NodeType, left, right);

                var members = newExpression.ToMembers();

                if (node.NodeType == ExpressionType.NotEqual)
                {
                    // TODO: check if it can be converted
                    if (members.Count > 1)
                        throw new InvalidOperationException("NotEqual operator can't be used on IDB: " + node.ToString());

                    PropertyInfo? integerToCompare = null;
                    if (members.First().Key is PropertyInfo prop
                        && prop.IsNullable() && prop.IsNumeric())
                        integerToCompare = prop;

                    // swap out the object key for the id key
                    if (members.First().Key is PropertyInfo prop2
                        && prop2.GetCustomAttribute<ForeignKeyAttribute>()?.Name is string foreign
                        && prop2.DeclaringType?.GetProperty(foreign) is PropertyInfo prop3
                        && prop3.IsNumeric())
                        integerToCompare = prop3;

                    // automatically fix u.User != null
                    if (integerToCompare != null
                        && (memberIsOnLeft ? left : right) is MemberExpression member
                        && ((memberIsOnLeft && right is ConstantExpression constant && constant.Value == null)
                        || (!memberIsOnLeft && left is ConstantExpression constant2 && constant2.Value == null)))
                    {
                        var realType = Nullable.GetUnderlyingType(integerToCompare.PropertyType) ?? integerToCompare.PropertyType;
                        var zeroIndex = Expression.Constant(Convert.ChangeType(0, realType), realType);
                        var newMember = Expression.MakeMemberAccess(member.Expression, integerToCompare);
                        var newerExpression = Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, left, zeroIndex);
                        Console.WriteLine("WARNING: converting expression from " + newExpression + " to " + newerExpression);
                        members = new Dictionary<MemberInfo, object?>{{ integerToCompare, 0}};
                        newExpression = newerExpression;
                    }
                    else
                        throw new InvalidOperationException("NotEqual operator can't be used on IDB: " + node.ToString());
                }


                // TODO: if we made it this far make a recording
                foreach(var member in members)
                {
                    CurrentRecording?.Comparators.Add((member.Key, node.NodeType, member.Value));
                }

                return newExpression;
            }


            if (node.IsBoolean())
            {
                if(node.NodeType == ExpressionType.OrElse)
                {
                    // TODO: they both get added
                    CurrentRecording?.AddBoth = true;
                }
                if (node.NodeType == ExpressionType.AndAlso)
                {
                    // TODO: one is comparator and one is secondary
                    CurrentRecording?.AddBoth = false;

                    // TODO: check if two AndAlso comparators we added in sequence, the latter can be plopped off into another list
                }
            }

            // make no changes

            return Expression.MakeBinary(node.NodeType, Visit(node.Left), Visit(node.Right));
        }


        protected override Expression VisitMember(MemberExpression node)
        {
            // 1. If this branch eventually leads back to the Lambda Parameter 'u',
            // we must keep it as a MemberExpression for our Whitelist/Index lookup.
            if (IsEntityRooted(node))
            {
                return base.VisitMember(node);
            }

            if (node.Expression is ParameterExpression parameter)
            {
                if (CurrentRecording?.MemberAccess != null
                    && parameter.Type != CurrentRecording.MemberAccess)
                {
                    throw new InvalidOperationException("Member accessors are not the same type, too complicated for IDB.");
                }

                CurrentRecording?.MemberAccess = parameter.Type;
                if (parameter.Type.Extends(typeof(Entities.Entity<>)))
                {
                    CurrentRecording?.EntityType = parameter.Type;
                }
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

            // TODO: i bet we could figure these out
            // 5. Block complex projections that break the 'Index' handshake
            //var relationalMethods = new[] { "Join", "GroupJoin", "SelectMany", "GroupBy" };
            //if (relationalMethods.Contains(node.Method.Name))
            //{
            //    throw new NotSupportedException(
            //        $"Relational operation '{node.Method.Name}' requires server-side execution.");
            //}


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
                    $"The method '{node.Method.Name}' is not supported in the IDB local context");
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
                    $"Blocking synchronous '{methodName}'. Use '{asyncEquivalentName}' instead.");
            }
        }





        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if(FilterFluff(node) is Expression queryable)
            {
                return queryable;
            }

            FilterBad(node);

            // Too aggressive
            //FilterBadNonAsync(node);


            // 2. Identify if this method has a Predicate (Func<T, bool>)
            var predicateArg = node.Arguments.FirstOrDefault(a => a.Type.Extends(typeof(Expression)));

            if (predicateArg != null)
            {
                // This is where we drill into the Lambda to fill our "MethodFilters" dictionary
                // TODO: record predicates and binary
                CurrentRecording = new RecordMethod();
                var possiblyEntity = predicateArg.Type.GetGenericArguments()
                    .FirstOrDefault()?.GetGenericArguments().FirstOrDefault();
                CurrentRecording?.MemberAccess = possiblyEntity;
                if (possiblyEntity?.Extends(typeof(Entities.Entity<>)) == true)
                {
                    CurrentRecording?.EntityType = possiblyEntity;
                }
            }



            // 2. Handle 'Where' - The Core Handshake
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                || node.Method.DeclaringType == typeof(Queryable)
                || node.Method.Name == nameof(Queryable.Where))
            {
                // We keep the 'Where' but its arguments (the lambda) 
                // will be cleaned by our VisitMember override.
                var callVisited = base.VisitMethodCall(node);

                if(CurrentRecording != null)
                {
                    if (!Recordings.TryAdd(node.Method, CurrentRecording))
                        throw new InvalidOperationException("Method call already in recordings, too complicated.");
                    CurrentRecording = null;
                }

                return callVisited;
            }

            throw new InvalidOperationException($"Method '{node.Method.Name}' is not part of the supported IDB method chain.");
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
