using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace DataLayer.Utilities.Extensions
{
    public static class ExpressionExtensions
    {

        private static Dictionary<string, string?> ToDictionary(this MemberInitExpression expression)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var binding in expression.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    // We extract the name and value without ever running the constructor
                    var value = Expression.Lambda(assignment.Expression).Compile().DynamicInvoke();
                    values[assignment.Member.Name] = value?.ToString();
                }
            }
            return values;
        }


        public static Dictionary<string, string?> ToDictionary(this NewExpression ne)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ne.Members?.Count; i++)
            {
                var value = Expression.Lambda(ne.Arguments[i]).Compile().DynamicInvoke();
                values[ne.Members[i].Name] = value?.ToString();
            }
            return values;
        }


        public static Dictionary<string, string?> ToDictionary<TDelegate>(this Expression<TDelegate> ex)
        {
            _ = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string?>? values;
            if (ex?.Body is NewExpression ne)
            {
                values = ne.ToDictionary();
            }
            else if (ex?.Body is MemberInitExpression mi)
            {
                values = mi.ToDictionary();
            }
            else if (ex?.Body is LambdaExpression le)
            {
                values = le.ToDictionary();
            }
            else if (ex?.Body is BinaryExpression bi)
            {
                values = bi.ToDictionary();
            }
            else
            {
                throw new InvalidOperationException("Can't do anything else with this " + ex?.Body.GetType() + ", frankly.");
            }
            return values;
        }


        private static Dictionary<string, string?> ToDictionary(this LambdaExpression expression)
        {
            return expression.ToMembers().ToDictionary(dkv => dkv.Key.Name, dkv => dkv.Value?.ToString());
        }


        private static Dictionary<MemberInfo, object?> ToMembers(this LambdaExpression expression)
        {
            var values = new Dictionary<MemberInfo, object?>();

            // The 'Body' of the Lambda is where the comparison logic lives
            ParseExpression(expression.Body, values);

            return values;
        }


        private static Dictionary<string, string?> ToDictionary(this BinaryExpression expression)
        {
            return expression.ToMembers().ToDictionary(dkv => dkv.Key.Name, dkv => dkv.Value?.ToString());
        }


        private static Dictionary<MemberInfo, object?> ToMembers(this BinaryExpression expression)
        {
            var values = new Dictionary<MemberInfo, object?>();

            // The 'Body' of the Lambda is where the comparison logic lives
            ParseExpression(expression, values);

            return values;
        }

        private static void ParseExpression(Expression expr, Dictionary<MemberInfo, object?> values)
        {
            if (expr is BinaryExpression binary)
            {
                if (binary.NodeType == ExpressionType.AndAlso)
                {
                    // Recursively check both sides of the &&
                    ParseExpression(binary.Left, values);
                    ParseExpression(binary.Right, values);
                }
                else if (binary.NodeType == ExpressionType.Equal)
                {
                    // We found a comparison: e.Member == Constant
                    MemberInfo? member = null;

                    // Handle e.Member (Left)
                    if (binary.Left is MemberExpression memberExpr)
                    {
                        member = memberExpr.Member;
                    }

                    object? value;
                    // Handle Constant (Right)
                    if (binary.Right is ConstantExpression constantExpr)
                    {
                        value = constantExpr.Value;
                    }
                    else
                    {
                        // If it's a variable capture, we compile and invoke to get the value
                        value = Expression.Lambda(binary.Right).Compile().DynamicInvoke();
                    }

                    if (member != null)
                    {
                        values[member] = value;
                    }
                }
            }
        }


        private static Dictionary<MemberInfo, object?> ToMembers(this MemberInitExpression expression)
        {
            var values = new Dictionary<MemberInfo, object?>();

            foreach (var binding in expression.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    // Directly use the Member metadata
                    var value = Expression.Lambda(assignment.Expression).Compile().DynamicInvoke();
                    values[assignment.Member] = value;
                }
            }
            return values;
        }


        public static Dictionary<MemberInfo, object?> ToMembers(this NewExpression ne)
        {
            var values = new Dictionary<MemberInfo, object?>();
            if (ne.Members == null) return values;

            for (int i = 0; i < ne.Members.Count; i++)
            {
                var value = Expression.Lambda(ne.Arguments[i]).Compile().DynamicInvoke();
                values[ne.Members[i]] = value;
            }
            return values;
        }

        public static Dictionary<MemberInfo, object?> ToMembers<TDelegate>(this Expression<TDelegate> ex)
        {
            if (ex?.Body is NewExpression ne)
                return ne.ToMembers();

            if (ex?.Body is MemberInitExpression mi)
                return mi.ToMembers();

            if (ex?.Body is LambdaExpression le)
                return le.ToMembers();

            if (ex?.Body is BinaryExpression bi)
                return bi.ToMembers();

            throw new InvalidOperationException("Can't do anything else with this " + ex?.Body.GetType() + ", frankly.");
        }


        public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(this Expression<TEntity> ex)
            where TEntity : class
        {
            return ToPredicate<TEntity>(ex.ToMembers());
        }

        public static Expression<Func<TEntity, bool>> ToPredicate<TEntity>(Dictionary<MemberInfo, object?> members)
            where TEntity : class
        {
            var type = typeof(TEntity);
            var parameter = Expression.Parameter(type, "e");

            // 1. Identify the Keys (Mirroring your logic)
            var keyProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<KeyAttribute>() != null ||
                            p.Name == "Id" ||
                            p.Name == $"{type.Name}Id")
                .ToList();

            if (keyProperties.Count == 0)
                throw new InvalidOperationException($"Entity {type.Name} has no identifiable Primary Key.");

            Expression? predicate = null;

            foreach (var prop in keyProperties)
            {
                // Check if our MemberInit dictionary actually contains this key
                if (!members.TryGetValue(prop, out var value))
                {
                    // In Arizona, an incomplete contract is unenforceable.
                    // In EF, an incomplete key is unqueryable.
                    throw new ArgumentException($"MemberInit is missing required Primary Key: {prop.Name}");
                }

                // Build: e.Prop == Value
                var left = Expression.Property(parameter, prop);
                var right = Expression.Constant(value, prop.PropertyType);
                var comparison = Expression.Equal(left, right);

                predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
            }

            return Expression.Lambda<Func<TEntity, bool>>(predicate!, parameter);
        }
    }
}
