using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DataLayer.Utilities.Extensions
{
    public static partial class ExpressionExtensions
    {
        /// <summary>
        /// Converts a string path (e.g. "User.Address.City") into a MemberExpression.
        /// Replaces the old GetExpressionRecursive.
        /// </summary>
        public static Expression ToMember(this Expression container, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return container;

            var parts = columnName.Split('.');
            Expression current = container;

            foreach (var part in parts)
            {
                // Use regex to split "Users[0]" into "Users" and "0"
                var match = BasicIndexerParser().Match(part);
                var name = match.Groups["name"].Value;
                var indexStr = match.Groups["index"].Value;

                // 1. Resolve the Member (Property/Field)
                var member = current.Type.GetMember(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    .FirstOrDefault()
                    ?? throw new ArgumentException($"Member {name} not found on {current.Type.Name}");

                current = Expression.MakeMemberAccess(current, member);

                // 2. Resolve the Indexer if [n] was provided
                if (!string.IsNullOrEmpty(indexStr) && int.TryParse(indexStr, out int index))
                {
                    if (current.Type.IsArray)
                    {
                        current = Expression.ArrayIndex(current, Expression.Constant(index));
                    }
                    else
                    {
                        // Look for the 'Item' indexer property (standard for List<T>, Dictionary, etc.)
                        var indexer = current.Type.GetProperty("Item");
                        if (indexer != null)
                        {
                            current = Expression.MakeIndex(current, indexer, [Expression.Constant(index)]);
                        }
                    }
                }
            }
            return current;
        }

        // TODO: QueryManager.Query(string).Any(u => !u.IsDeleted) 
        // TODO: QueryManager.Query<User>().Where(string) 
        // TODO: QueryManager.Query<User>().OrderBy(string) 
        // 
        public static Expression ToMember(this Expression container, string columnName, string term)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return container;

            var parts = columnName.Split('.');
            Expression current = container;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var match = BasicIndexerParser().Match(part);
                var name = match.Groups["name"].Value;
                var indexStr = match.Groups["index"].Value;

                // 1. Resolve Property/Field
                var member = current.Type.GetMember(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase).FirstOrDefault() ?? throw new ArgumentException($"Member {name} not found.");
                current = Expression.MakeMemberAccess(current, member);

                // 2. Handle [0] Indexers (Direct Access)
                if (!string.IsNullOrEmpty(indexStr) && int.TryParse(indexStr, out int index))
                {
                    current = current.Type.IsArray
                        ? Expression.ArrayIndex(current, Expression.Constant(index))
                        : Expression.MakeIndex(current, current.Type.GetProperty("Item"), new[] { Expression.Constant(index) });
                    continue;
                }

                // 3. Handle Collections (Any matching)
                // Check if type is IEnumerable but NOT string
                var elementType = GetEnumerableElementType(current.Type);
                if (elementType != null && i < parts.Length - 1)
                {
                    // We are at a collection (e.g., "Orders") and have more path (e.g., "Amount")
                    var innerParam = Expression.Parameter(elementType, "i");
                    var remainingPath = string.Join(".", parts.Skip(i + 1));

                    // Recursive call for the inner part: i.Amount == term
                    var innerBody = innerParam.ToMember(remainingPath, term).ToEquality(term);

                    var anyLambda = Expression.Lambda(innerBody, innerParam);

                    // Return Enumerable.Any(current, anyLambda)
                    return Expression.Call(
                        typeof(Enumerable), "Any", [elementType],
                        current, anyLambda
                    );
                }
            }

            return current;
        }

        private static Type? GetEnumerableElementType(Type type)
        {
            if (type == typeof(string)) return null;
            if (type.IsArray) return type.GetElementType();
            return type.GetInterfaces()
                       .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                       ?.GetGenericArguments().FirstOrDefault();
        }




        public static Expression<Func<object, object?>>? ToAccessor(this string columnName, Type entityType)
        {
            // Call the generic version via reflection
            var genericMethod = typeof(ExpressionExtensions)
                .GetMethod(nameof(ToAccessor), [typeof(string)])?
                .MakeGenericMethod(entityType);

            var genericExpr = genericMethod?.Invoke(null, [columnName]);

            if (genericExpr == null) return null;

            // We must transform Expression<Func<TEntity, object>> 
            // into Expression<Func<object, object>>
            var param = Expression.Parameter(typeof(object), "ent");
            var castParam = Expression.Convert(param, entityType);

            // Invoke the generic expression using the casted parameter
            var invocation = Expression.Invoke((Expression)genericExpr, castParam);

            return Expression.Lambda<Func<object, object?>>(invocation, param);
        }

        public static Expression<Func<TEntity, object?>> ToAccessor<TEntity>(this string columnName)
        {
            var type = typeof(TEntity);
            var entityParam = Expression.Parameter(type, "ent");

            // 1. Get the MemberInfo (Property or Field)
            MemberInfo? member = type.GetProperty(columnName) as MemberInfo ?? type.GetField(columnName);

            if (member == null)
            {
                return ent => null;
            }

            // 2. Create the property/field access: ent.ColumnName
            var access = Expression.MakeMemberAccess(entityParam, member);

            // 3. Box to object (since properties/fields might be ValueTypes)
            var boxedAccess = Expression.Convert(access, typeof(object));

            return Expression.Lambda<Func<TEntity, object?>>(boxedAccess, entityParam);
        }



        private static Dictionary<MemberInfo, object?> ToMembers(this ConditionalExpression expression)
        {
            var values = new Dictionary<MemberInfo, object?>();

            // The 'Body' of the Lambda is where the comparison logic lives
            ParseExpression(expression, values);

            return values;
        }

        private static Dictionary<MemberInfo, object?> ToMembers(this LambdaExpression expression)
        {
            var values = new Dictionary<MemberInfo, object?>();

            // The 'Body' of the Lambda is where the comparison logic lives
            ParseExpression(expression.Body, values);

            return values;
        }


        public static Dictionary<string, string?> ToDictionary(this Expression expression)
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


        public static bool IsScalar(this Expression expression)
        {
            if (expression is MethodCallExpression m)
            {
                var name = m.Method.Name;
                return name == "Count" || name == "LongCount" ||
                       name == "Min" || name == "Max" ||
                       name == "Sum" || name == "Average" ||
                       name == "Any" || name == "All" ||
                       name == "Contains";
            }
            return false;
        }

        /// <summary>
        /// Returns true if the expression produces a single result 
        /// (Scalar, Element operator, or Boolean check).
        /// </summary>
        public static bool IsTerminal(this Expression expression)
        {
            return expression.IsSingular() || expression.IsScalar();
        }


        public static bool IsDefault(this Expression expression)
        {
            if (expression is MethodCallExpression m)
            {
                var name = m.Method.Name;
                return name == "FirstOrDefault" ||
                       name == "SingleOrDefault" ||
                       name == "LastOrDefault";
            }
            return false;
        }


        public static bool IsSingular(this Expression expression)
        {
            if (expression is MethodCallExpression m)
            {
                var name = m.Method.Name;
                return name == "FirstOrDefault" || name == "First" ||
                       name == "SingleOrDefault" || name == "Single" ||
                       name == "LastOrDefault" || name == "Last";
            }
            return false;
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

                foreach(var arg in mc.Arguments)
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


        private static void ExtractComparison(BinaryExpression binary, Dictionary<MemberInfo, object?> values)
        {
            var left = Unbox(binary.Left);
            var right = Unbox(binary.Right);

            // 1. Try to find the Entity Member on either side
            if (TryGetEntityMember(left, out var entityMember))
            {
                values[entityMember!] = ResolveValue(right);
            }
            else if (TryGetEntityMember(right, out var entityMemberRight))
            {
                values[entityMemberRight!] = ResolveValue(left);
            }
            else
            {
                // 2. RECURSIVE WRAPPER LOGIC:
                // If neither side is a direct entity property, one side might be a 
                // MemberExpression that EVALUATES to a Lambda/Expression.
                if (left is MemberExpression me && typeof(Expression).IsAssignableFrom(me.Type))
                {
                    var innerExpr = ResolveValue(me) as Expression;
                    ParseExpression(innerExpr, values);
                }
                if (right is MemberExpression re && typeof(Expression).IsAssignableFrom(re.Type))
                {
                    var innerExpr = ResolveValue(re) as Expression;
                    ParseExpression(innerExpr, values);
                }
            }
        }

        private static bool TryGetEntityMember(Expression expr, out MemberInfo? member)
        {
            member = null;
            if (Unbox(expr) is MemberExpression me && IsParameterResource(me))
            {
                member = me.Member;
                return true;
            }
            return false;
        }

        private static bool IsParameterResource(MemberExpression me)
        {
            // Drills down to the root. If the root is 's' in 's => s.Name', it's an entity property.
            // If the root is a 'Constant' (the closure class), it's a value/variable.
            Expression? root = me.Expression;
            while (root is MemberExpression next) root = next.Expression;
            while (root is UnaryExpression unary) root = unary.Operand;

            return root is ParameterExpression;
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
            return ToMembers(ex.Body);
        }

        public static Dictionary<MemberInfo, object?> ToMembers(this Expression ex)
        {
            if(typeof(Expression<>).IsCompatibleWith(ex.GetType()))
                return (ex as dynamic).Body.ToMembers();

            if (ex is NewExpression ne)
                return ne.ToMembers();

            if (ex is MemberInitExpression mi)
                return mi.ToMembers();

            if (ex is LambdaExpression le)
                return le.ToMembers();

            if (ex is BinaryExpression bi)
                return bi.ToMembers();

            var dictionary = new Dictionary<MemberInfo, object?>();
            ParseExpression(ex, dictionary);
            if(dictionary.Count > 0) return dictionary;  

            throw new InvalidOperationException("Can't do anything else with this " + ex.GetType() + ", frankly.");
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

        [GeneratedRegex(@"(?<name>[^\[]+)(?:\[(?<index>\d+)\])?")]
        private static partial Regex BasicIndexerParser();
    }
}
