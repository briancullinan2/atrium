
namespace Extensions.PrometheusTypes;

public static partial class ExpressionExtensions
{

    private static Dictionary<MemberInfo, object?> ToMembers(this BinaryExpression expression)
    {
        var values = new Dictionary<MemberInfo, object?>();

        // The 'Body' of the Lambda is where the comparison logic lives
        ParseExpression(expression, values);

        return values;
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
        if (typeof(Expression<>).Extends(ex.GetType()))
            return (ex as dynamic).Body.ToMembers();

        if (ex is NewExpression ne)
            return ne.ToMembers();

        if (ex is MemberInitExpression mi)
            return mi.ToMembers();

        if (ex is LambdaExpression le)
            return le.ToMembers();

        if (ex is BinaryExpression bi)
            return bi.ToMembers();

        if (ex is ConstantExpression co
            && co.Value?.GetType().Extends(typeof(IAsyncQueryable<>)) == true)
            return [];

        var dictionary = new Dictionary<MemberInfo, object?>();
        ParseExpression(ex, dictionary);
        if (dictionary.Count > 0) return dictionary;

        throw new InvalidOperationException("Can't do anything else with this " + ex.GetType() + ", frankly.");
    }




    private static Dictionary<MemberInfo, object?> ToMembers(this object? any)
    {
        if (any == null) return [];
        var properties = any.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p =>
                !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                && !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute))
                )
            .OrderBy(p => p.Name)
            .ToList();
        return properties.ToDictionary<PropertyInfo, MemberInfo, object?>(p => (MemberInfo)p, p => p.GetValue(any));
    }




}
