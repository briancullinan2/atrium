using System;
using System.Collections.Generic;
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
            else
            {
                throw new InvalidOperationException("Can't do anything else, frankly.");
            }
            return values;
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

            throw new InvalidOperationException("Expression body must be NewExpression or MemberInitExpression.");
        }
    }
}
