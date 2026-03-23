using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Xml;
using System.Xml.Linq;


namespace DataLayer.Utilities.Extensions
{
    public static class LinqExtensions
    {
        public static PropertyInfo? FindProperty<TModel, TReturn>(this Expression<Func<TModel, TReturn>> expression)
        {
            // Unrolling the 'Advanced' type from the expression tree
            if (expression.Body is MemberExpression member && member.Member is PropertyInfo prop)
            {
                return prop;
            }

            // Handle boxing if TReturn is 'object' but the property is a struct
            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression innerMember)
            {
                return innerMember.Member as PropertyInfo;
            }

            throw new ArgumentException("Could not descend the code tree to find a valid property.");
        }

        private static readonly int _maxDepth = 2;
        private static readonly int _maxExpressionDepth = 10;

        public static XDocument ToXDocument(this Expression expression)
        {
            return new XDocument(VisitToXml(expression, 0, 0));
        }

        public static XElement VisitToXml(object? node, int currentDepth, int expressionDepth)
        {
            if (node == null) return new XElement("Null");

            var type = node.GetType();
            var element = new XElement(type.Name.ToSafe());

            if (expressionDepth >= _maxExpressionDepth)
            {
                element.Add(new XAttribute("DepthReached", "True"));
            }
            if (currentDepth >= _maxDepth || expressionDepth >= _maxExpressionDepth)
            {
                //element.Add(new XAttribute("DepthReached", "True"));
                // For MethodInfo/Type, we still want the basic string even if depth is reached
                element.Value = node.ToString() ?? "";
                return element;
            }


            if (node is IEnumerable list2 && node is not string)
            {
                foreach (var item in list2)
                {
                    // Add items directly to the parent element
                    element.Add(VisitToXml(item, currentDepth + 1, expressionDepth + 1));
                }
                // If it's a collection, we usually don't need its internal properties 
                // (like Count or Capacity), so we can return here.
                return element;
            }

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            //if (node is UnaryExpression unary)
            //{
            //    Console.WriteLine(unary.Operand.ToString());
            //    Console.WriteLine(string.Join(" ", props.Select(p => p.Name).ToList()));
            //}
            foreach (var prop in props)
            {
                if (prop.GetIndexParameters().Length > 0) continue;

                if (prop.Name == "ImplementedInterfaces" || prop.Name == "DeclaredProperties"
                    || prop.Name == "DeclaredMethods" || prop.Name == "CanReduce" || prop.Name == "TailCall") continue;

                try
                {
                    var value = prop.GetValue(node);

                    // Treat Expression, MethodInfo, and Type as "Complex" to recurse into them
                    if (value is Expression)
                    {
                        var propElement = new XElement(prop.Name);
                        propElement.Add(VisitToXml(value, currentDepth, expressionDepth + 1));
                        element.Add(propElement);
                    }
                    else if (value is MethodBase method)
                    {
                        var propElement = new XElement(prop.Name);
                        var methodInfo = VisitToXml(value, currentDepth + 1, expressionDepth);
                        propElement.Add(methodInfo);
                        var parameters = method.GetParameters();
                        methodInfo.Add(new XAttribute("ParamCount", parameters.Length));
                        if (method.IsGenericMethod)
                        {
                            var generics = new XElement("GenericArguments");
                            foreach (var arg in method.GetGenericArguments())
                            {
                                // One level of recursion for the arg is usually safe and necessary
                                generics.Add(new XElement("Type", new XAttribute("AssemblyQualifiedName", arg.AssemblyQualifiedName ?? "")));
                            }
                            methodInfo.Add(generics);
                        }
                        // Optional: Serialize the parameter types to be 100% sure on overloads
                        var paramsEl = new XElement("Parameters");
                        foreach (var p in parameters)
                        {
                            paramsEl.Add(new XElement("Parameter",
                                new XAttribute("Type", p.ParameterType.AssemblyQualifiedName ?? "")));
                        }
                        methodInfo.Add(paramsEl);
                        element.Add(propElement);
                    }
                    else if (value is ValueBuffer buffer)
                    {
                        var propElement = new XElement(prop.Name, 
                            new XAttribute("Count", buffer.Count
                        ));
                        for (var i = 0; i < buffer.Count; i++)
                        {
                            var item = buffer[0];
                            if(item == null || item.GetType().IsSimple() == true)
                            {
                                propElement.Add(new XElement(item?.GetType().Name.ToSafe() ?? "Null",
                                    new XAttribute("Value", item?.ToString() ?? string.Empty),
                                    new XAttribute("ValueType", item?.GetType().AssemblyQualifiedName ?? "")
                                ));
                            }
                            else
                            {
                                propElement.Add(VisitToXml(item, currentDepth, expressionDepth + 1));
                            }
                        }
                        element.Add(propElement);
                    }
                    /*else if (value is PropertyInfo property)
                    {
                        var propElement = new XElement(prop.Name);
                        var methodInfo = VisitToXml(value, currentDepth + 1, expressionDepth);
                        propElement.Add(methodInfo);
                        var parameters = property.GetIndexParameters();
                        methodInfo.Add(new XAttribute("ParamCount", parameters.Length));
                        if (property.PropertyType.IsGenericTypeDefinition)
                        {
                            var generics = new XElement("GenericArguments");
                            foreach (var arg in property.PropertyType.GetGenericArguments())
                            {
                                // One level of recursion for the arg is usually safe and necessary
                                generics.Add(new XElement("Type", new XAttribute("AssemblyQualifiedName", arg.AssemblyQualifiedName ?? "")));
                            }
                            methodInfo.Add(generics);
                        }
                        // Optional: Serialize the parameter types to be 100% sure on overloads
                        var paramsEl = new XElement("Parameters");
                        foreach (var p in parameters)
                        {
                            paramsEl.Add(new XElement("Parameter",
                                new XAttribute("Type", p.ParameterType.AssemblyQualifiedName ?? "")));
                        }
                        methodInfo.Add(paramsEl);
                        element.Add(propElement);
                    }*/
                    else if (value is Type t)
                    {
                        var typeElement = new XElement(prop.Name);
                        // Don't recurse! Just add the critical identification data.
                        typeElement.Add(new XAttribute("Name", t.Name));
                        typeElement.Add(new XAttribute("FullName", t.FullName ?? ""));
                        typeElement.Add(new XAttribute("Namespace", t.Namespace ?? ""));
                        typeElement.Add(new XAttribute("AssemblyQualifiedName", t.AssemblyQualifiedName ?? ""));

                        // If it's a generic type (like List<Pack>), you might need the generic arguments
                        if (t.IsGenericType)
                        {
                            var generics = new XElement("GenericArguments");
                            foreach (var arg in t.GetGenericArguments())
                            {
                                // One level of recursion for the arg is usually safe and necessary
                                generics.Add(new XElement("Type", new XAttribute("AssemblyQualifiedName", arg.AssemblyQualifiedName ?? "")));
                            }
                            typeElement.Add(generics);
                        }

                        element.Add(typeElement);
                    }
                    else if (value is IEnumerable list && value is not string)
                    {
                        var propElement = new XElement(prop.Name);
                        foreach (var item in list)
                        {
                            propElement.Add(VisitToXml(item, currentDepth, expressionDepth + 1));
                        }
                        element.Add(propElement);

                    }
                    else if (value == null || value.GetType().IsSimple() == true)
                    {
                        element.Add(new XAttribute(prop.Name, value?.ToString() ?? "null"));
                    }
                    else
                    {
                        var propElement = new XElement(prop.Name);
                        propElement.Add(VisitToXml(value, currentDepth + 1, expressionDepth));
                        element.Add(propElement);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(prop.Name);
                    Console.WriteLine(ex);
                }
            }

            return element;
        }


        // A registry to map NodeType to the correct Factory Method
        private static readonly Dictionary<string, ExpressionType> _nodeTypeLookup = Enum.GetValues<ExpressionType>()
            .Cast<ExpressionType>()
            .ToDictionary(t => t.ToString(), t => t);
        private static readonly Dictionary<ExpressionType, List<Tuple<MethodInfo, List<ParameterInfo>>>> _factoryMap = typeof(Expression)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            // We filter for methods that take Expression arguments to avoid overloads like 'Constant'
            .Where(m => _nodeTypeLookup.ContainsKey(m.Name))
            .GroupBy(m => _nodeTypeLookup[m.Name])
            .ToDictionary(g => g.Key, g => g.Select(g => new Tuple<MethodInfo, List<ParameterInfo>>(g, [.. g.GetParameters()])).ToList());
        private static readonly Dictionary<ExpressionType, ExpressionFactory> _elementMap =
            new() {
                { ExpressionType.Call, BuildMethodCall },
                { ExpressionType.Parameter, BuildParameter },
                { ExpressionType.Constant, BuildConstant },
                { ExpressionType.Quote, BuildUnary },
                { ExpressionType.Lambda, BuildLambda },
                { ExpressionType.Equal, BuildLeftRight },
                { ExpressionType.Convert, BuildUnary },
                { ExpressionType.MemberAccess, BuildProperty },
                { ExpressionType.OrElse, BuildLeftRight },
                { ExpressionType.Index, BuildIndex },
                { ExpressionType.Invoke, BuildInvocation },
                { ExpressionType.Conditional, BuildConditional },
                { ExpressionType.NewArrayInit, BuildNewArrayInit },

                //{ExpressionType.Extension, BuildExtension}
            };

        public delegate Expression? ExpressionFactory(XElement el, Func<XElement, Expression?> ToExpression);
        private static Expression BuildNewArrayInit(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Get the array type (e.g., System.Object[])
            var typeName = el.Element("Type")?.Attribute("AssemblyQualifiedName")?.Value
                           ?? el.Element("Type")?.Value;
            var arrayType = Type.GetType(typeName!)
                            ?? throw new InvalidOperationException($"Array type not found: {typeName}");

            // 2. Get the element type (System.Object)
            var elementType = arrayType.GetElementType() ?? typeof(object);

            // 3. Resolve all the expressions inside the { ... }
            var expressions = el.Element("Expressions")?.Elements()
                .Select(x => ToExpression(x)!)
                .ToList() ?? [];

            return Expression.NewArrayInit(elementType, expressions);
        }

        private static Expression BuildConditional(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var test = ToExpression(el.Element("Test")?.Elements().First()!)!;
            var ifTrue = ToExpression(el.Element("IfTrue")?.Elements().First()!)!;
            var ifFalse = ToExpression(el.Element("IfFalse")?.Elements().First()!)!;

            return Expression.Condition(test, ifTrue, ifFalse);
        }

        private static Expression? BuildProperty(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var expressionEl = (el.Element("Expression")?.Elements().FirstOrDefault()) ?? throw new InvalidOperationException("Could not resolve property expression on " + el);
            var expression = ToExpression(expressionEl);
            var memberInfo = (el.Element("Member")?.Elements().FirstOrDefault()) ?? throw new InvalidOperationException("Could not resolve property member on " + el);
            PropertyInfo? propertyInfo = ResolveMetadata(typeof(PropertyInfo), memberInfo.Value, memberInfo) as PropertyInfo ?? throw new InvalidOperationException("Could not resolve method info on " + el);
            return Expression.MakeMemberAccess(expression, propertyInfo);
        }


        private static Expression? BuildLeftRight(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var rightEl = el.Element("Right")?.Elements().FirstOrDefault();
            var leftEl = el.Element("Left")?.Elements().FirstOrDefault();
            if (rightEl == null || leftEl == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }
            var rightOperand = ToExpression(rightEl);
            var leftOperand = ToExpression(leftEl);
            if (rightOperand == null || leftOperand == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }
            if (el.Attribute("NodeType")?.Value == "Equal")
                return Expression.Equal(leftOperand, rightOperand);
            if (el.Attribute("NodeType")?.Value == "OrElse")
                return Expression.OrElse(leftOperand, rightOperand);
            throw new InvalidOperationException("Node Type not supported." + el.Attribute("NodeType"));
        }

        private static Expression? BuildLambda(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var parametersEl = el.Element("Parameters")?.Elements();
            var bodyEl = (el.Element("Body")?.Elements().FirstOrDefault()) ?? throw new InvalidOperationException("Could not resolve lambda body on " + el);
            IEnumerable<ParameterExpression>? parameters = parametersEl?.Select(p => ToExpression(p)).Cast<ParameterExpression>();
            var realBody = ToExpression(bodyEl) ?? throw new InvalidOperationException("Could not resolve lambda body on " + el);
            return Expression.Lambda(realBody, parameters);
        }


        private static Expression? BuildUnary(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var operandEl = el.Element("Operand")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException($"Missing operand: {el}");

            var operand = ToExpression(operandEl)
                ?? throw new InvalidOperationException("Operand resolution failed.");

            var nodeType = el.Attribute("NodeType")?.Value;

            if (nodeType == "Quote") return Expression.Quote(operand);

            if (nodeType == "Convert")
            {
                // 1. Prioritize AssemblyQualifiedName for Generics/Nullables
                var typeEl = el.Element("Type");
                var typeName = typeEl?.Attribute("AssemblyQualifiedName")?.Value
                              ?? typeEl?.Attribute("FullName")?.Value
                              ?? throw new InvalidOperationException("Missing type metadata.");

                var resolvedType = Type.GetType(typeName);

                // 2. Fallback for common types if reflection is being finicky
                if (resolvedType == null && typeName.Contains("Nullable"))
                {
                    // Handle manual assembly loading or specific mappings if needed
                    throw new InvalidOperationException($"Type load failed for: {typeName}");
                }

                return Expression.Convert(operand, resolvedType!);
            }

            throw new NotSupportedException($"Unary NodeType {nodeType} not implemented.");
        }


        private static Expression BuildIndex(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Resolve the target object (the thing being indexed)
            var instanceEl = el.Element("Object")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("IndexExpression missing Object target.");
            var instance = ToExpression(instanceEl)!;

            // 2. Resolve the Indexer (PropertyInfo)
            var indexerEl = el.Element("Indexer")?.Element("Runtimepropertyinfo")
                    ?? throw new InvalidOperationException("IndexExpression missing Indexer info.");
            var declaringTypeName = indexerEl.Element("DeclaringType")?.Attribute("AssemblyQualifiedName")?.Value;
            var declaringType = Type.GetType(declaringTypeName!) 
                ?? throw new InvalidOperationException("IndexExpression missing declaring type.");

            var propertyName = indexerEl.Attribute("Name")?.Value ?? "Item";
            var reflectedTypeName = indexerEl.Element("ReflectedType")?.Attribute("AssemblyQualifiedName")?.Value;
            var reflectedType = Type.GetType(reflectedTypeName!)
                ?? throw new InvalidOperationException($"Could not resolve ReflectedType: {reflectedTypeName}");

            // Use reflection to get the PropertyInfo for the indexer
            var propertyInfo = reflectedType.GetProperty(propertyName);

            // 3. Resolve the Arguments (the index values)
            var argumentsEl = el.Element("Arguments")?.Elements() ?? [];
            var arguments = argumentsEl.Select(x => ToExpression(x)!).ToList();

            // 4. Build the Index node
            return Expression.MakeIndex(instance, propertyInfo, arguments);
        }




        private static T? RebuildMember<T>(XElement el) where T : MemberInfo
        {
            // 1. Metadata Extraction (Your logic, cleaned up)
            var declEl = el.Element("DeclaringType");
            var methodName = el.Attribute("Name")?.Value;
            var typeName = declEl?.Attribute("AssemblyQualifiedName")?.Value ?? declEl?.Value;

            if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(typeName))
                throw new InvalidOperationException($"Incomplete method metadata: {el}");

            var declaringType = Type.GetType(typeName)
                ?? throw new InvalidOperationException($"Could not load type: {typeName}");

            // 2. Parameter Count Extraction
            var paramsCount = int.TryParse(el.Attribute("ParamsCount")?.Value, out var pc)
                ? pc
                : el.Element("Parameters")?.Elements().Count() ?? 0;


            if (typeof(PropertyInfo).IsAssignableFrom(typeof(T)))
            {
                var properties = declaringType.GetProperties()
                    .Where(m => m.Name == methodName && paramsCount == 0 || m.GetIndexParameters().Length == paramsCount)
                    .ToList();

                if (properties.Count == 0)
                    throw new InvalidOperationException($"Method {methodName} with {paramsCount} params not found on {typeName}");

                return properties.First() as T;
            }
            else if (typeof(MethodInfo).IsAssignableFrom(typeof(T)))
            {

                // 3. Smart Method Selection
                // We filter by name and count, but we prioritize Generic Method Definitions
                // because that's what Select/Where/FirstOrDefault are.
                var methods = declaringType.GetMethods()
                    .Where(m => m.Name == methodName && m.GetParameters().Length == paramsCount)
                    .ToList();

                if (methods.Count == 0)
                    throw new InvalidOperationException($"Method {methodName} with {paramsCount} params not found on {typeName}");

                // If there's an ambiguity (e.g., Select vs Select with index), 
                // the XML's GenericArguments will tell us if we're looking for the generic one.
                var genericsEl = el.Element("GenericArguments");
                MethodInfo method;

                if (genericsEl != null && genericsEl.HasElements)
                {
                    method = methods.FirstOrDefault(m => m.IsGenericMethodDefinition)
                        ?? methods.First();

                    var genericTypes = genericsEl.Elements()
                        .Select(g => Type.GetType(g.Attribute("AssemblyQualifiedName")?.Value ?? g.Value))
                        .Cast<Type>()
                        .ToArray();

                    method = method.MakeGenericMethod(genericTypes);
                }
                else
                {
                    method = methods.First();
                }

                return method as T;
            }
            else throw new InvalidOperationException("Don't know this sort of member.");
        }



        private static InvocationExpression BuildInvocation(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // The 'Expression' being invoked (usually a Lambda)
            var expressionEl = el.Element("Expression")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Invoke missing target expression");
            var target = ToExpression(expressionEl)!;

            // The arguments being passed to that lambda
            var args = el.Element("Arguments")?.Elements()
                .Select(x => ToExpression(x)!)
                .ToList() ?? [];

            return Expression.Invoke(target, args);
        }


        /*
        public static Expression? RebuildInvoker(XElement el, Func<XElement, Expression?> ToExpression)
        {
            string nodeType = el.Attribute("NodeType")?.Value ?? "";

            return nodeType switch
            {
                "Call" => BuildMethodCall(el, ToExpression),
                "Invoke" => BuildInvocation(el, ToExpression),
                "Lambda" => BuildLambda(el, ToExpression),
                "Constant" => BuildConstant(el, ToExpression), // etc...
                _ => throw new NotSupportedException($"NodeType {nodeType} not implemented.")
            };
        }
        */


        private static Expression BuildMethodCall(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Resolve the actual MethodInfo (Where, Select, etc.)
            var methodEl = el.Element("Method")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing Method metadata");

            var methodInfo = ResolveMetadata(typeof(MethodInfo), null, methodEl) as MethodInfo
                ?? throw new InvalidOperationException("MethodInfo resolution failed");

            // 2. Resolve the Instance (The 'Object' attribute in your XML)
            // For LINQ extensions (static), this is usually null.
            var instanceEl = el.Element("Object")?.Elements().FirstOrDefault();
            var instance = instanceEl != null ? ToExpression(instanceEl) : null;

            // 3. Resolve Arguments
            var args = el.Element("Arguments")?.Elements()
                .Select(x => ToExpression(x)!)
                .ToList() ?? [];

            // 4. Use the static factory directly - much cleaner than a factoryMap
            return Expression.Call(instance, methodInfo, args);
        }


        private static object? ResolveMetadata(Type targetType, string? value, XElement el)
        {
            if (targetType == typeof(Type) && value != null)
                return Type.GetType(value);

            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(ExpressionType) && value != null)
                return Enum.Parse<ExpressionType>(value);

            if (targetType == typeof(MethodInfo))
                return RebuildMember<MethodInfo>(el);
            

            if (targetType == typeof(PropertyInfo))
                return RebuildMember<PropertyInfo>(el);


            if (targetType == typeof(ValueBuffer))
            {
                var values = el.Elements().Select(e => {
                    var typeName = e.Attribute("ValueType")?.Value
                        ?? throw new InvalidOperationException("Could not resolve type name on: " + e);
                    var entityType = Type.GetType(typeName)
                        ?? throw new InvalidOperationException("Could not resolve type on: " + el);
                    if (e.Attribute("Value")?.Value == "Null")
                        return null;
                    else if (entityType.IsSimple())
                        return Convert.ChangeType(e.Attribute("Value")?.Value, entityType);
                    else 
                        return ResolveMetadata(entityType, e.Attribute("Value")?.Value, e);
                }).ToArray();
                return new ValueBuffer(values);
            }


            throw new InvalidOperationException("Don't know that type: " + targetType);
        }



        private static Expression? BuildExtension(XElement el, DbContext context, out IQueryable? set)
        {
            var typeName = (el.Element("ElementType")?.Attribute("AssemblyQualifiedName")?.Value)
                ?? throw new InvalidOperationException("Could not resolve extension type on: " + el);
            
            var entityType = Type.GetType(typeName)
                ?? throw new InvalidOperationException("Could not resolve type on: " + el);

            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set))
                    ?.MakeGenericMethod(entityType)
                    ?? throw new InvalidOperationException("Could not render set creator in context: " + el);
            set = setMethod.Invoke(context, []) as IQueryable;
            return set?.Expression;
            /*

    #pragma warning disable EF1001 // Internal EF Core API usage.
                var entityQueryableType = typeof(EntityQueryable<>).MakeGenericType(entityType);
    #pragma warning restore EF1001 // Internal EF Core API usage.

                set = (IQueryable)Activator.CreateInstance(
                    entityQueryableType,
                    [context, Expression.Constant(null, entityQueryableType)] // Root expression
                )!;

                return set.Expression;
            */
        }



        private static ConstantExpression BuildConstant(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var typeName = (el.Element("Type")?.Attribute("AssemblyQualifiedName")?.Value) 
                ?? throw new InvalidOperationException("Could not resolve constant type attribute on: " + el);
            var type = Type.GetType(typeName)
                ?? throw new InvalidOperationException("Could not resolve constant type on: " + el);

            if(el.Element("Value") is XElement complex)
            {
                // TODO: 
                var val = ResolveMetadata(type, el.Attribute("Value")?.Value, complex);
                return Expression.Constant(Convert.ChangeType(val, type), type);
            } else if (el.Attribute("Value")?.Value is string val)
            {
                return Expression.Constant(Convert.ChangeType(val, type), type);
            }
            throw new InvalidOperationException("Cannot extract constant value.");
        }

        // Keep a cache of parameters during a single Reconstruction pass
        private static readonly Dictionary<string, ParameterExpression> _parameters = [];

        private static ParameterExpression BuildParameter(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var name = el.Element("Name")?.Value ?? "x";
            var typeName = (el.Element("Type")?.Attribute("FullName")?.Value) ?? throw new InvalidOperationException("Could not resolve parameter type on: " + el); // From your 'fluffy' reflection
            var type = Type.GetType(typeName) ?? typeof(object);

            // Important: Re-use the same ParameterExpression object for the same name
            if (!_parameters.TryGetValue(name, out var parameter))
            {
                parameter = Expression.Parameter(type, name);
                _parameters.Add(name, parameter);
            }
            return parameter;
        }




        private static Tuple<Expression?, IQueryable?> DumbToExpressionOutWrapper(XElement el, DbContext context)
        {
            var result = ToExpression(el, context, out var outish);
            return new Tuple<Expression?, IQueryable?>(result, outish);
        }

        public static Expression? ToExpression(this XElement el, DbContext context, out IQueryable? set)
        {
            set = null;
            var typeStr = el.Attribute("NodeType")?.Value;
            if (typeStr == null || !_nodeTypeLookup.TryGetValue(typeStr, out var nodeType))
                return null;

            // Special cases: Parameters and Constants usually need manual handling 
            // because they don't follow the "Children as Expressions" rule perfectly.
            if (nodeType == ExpressionType.Extension)
            {
                var typeName = el.Element("ElementType")?.Attribute("AssemblyQualifiedName")?.Value;
                if (typeName?.Contains("DataLayer") == true)
                {
                    return BuildExtension(el, context, out set); // Swap placeholder for Real DB source
                }
            }
            IQueryable? outish = null;
            if (_elementMap.TryGetValue(nodeType, out var factoryMethod))
            {
                var result = factoryMethod.Invoke(el, el2 =>
                {
                    var result = DumbToExpressionOutWrapper(el2, context);
                    if (result.Item2 != null) outish = result.Item2;
                    return result.Item1 as Expression;
                }) as Expression;
                if (outish != null) set = outish;
                return result;
            }

            throw new NotSupportedException($"No factory found for {nodeType}");
        }



        public static string ToSerialized(IQueryable query)
        {
            return ToXDocument(query.Expression).ToString();
        }

        public static async Task<object?> ToQueryable(string query, StorageType? persist = StorageType.Ephemeral)
        {
            if (QueryManager.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            using var scope = QueryManager.Service.CreateScope();
            var manager = QueryManager.Service.GetRequiredService<IQueryManager>();
            var context = manager.GetContext(persist ?? manager.EphemeralStorage) 
                ?? throw new InvalidOperationException("Database context failed.");


            await context.InitializeIfNeeded();

            //var provider = ((IQueryable)context.Set<Entities.User>()).Provider;

            using XmlReader reader = XmlReader.Create(new StringReader(query));
            _ = reader.MoveToContent();
            XElement root = (XElement)XNode.ReadFrom(reader);

            // 1. Clear the parameter cache for this specific run
            _parameters.Clear();

            // 2. Reconstruct the raw Expression tree
            // TODO: fix this, won't know how until i debug expressions and see what parts of the trees it can show in
            Expression? finalExpression = root.ToExpression(context, out IQueryable? set) 
                ?? throw new InvalidOperationException("Could not convert expression document to Queryable: " + query);
            if (typeof(IEnumerable).IsAssignableFrom(finalExpression.Type) && finalExpression.Type != typeof(string))
            {
                // It's a sequence - force materialization to avoid SingleQueryingEnumerable leaks
                var finalQueryable = set?.Provider.CreateQuery(finalExpression);

                var stringExpr = finalQueryable?.Expression.ToString();
                Console.WriteLine("Querying serialized: " + stringExpr);

                // Force ToList to materialize it before the context is disposed
                return typeof(Enumerable)
                    .GetMethod(nameof(Enumerable.ToList))
                    ?.MakeGenericMethod(finalExpression.Type.GenericTypeArguments[0]) // Or the target type
                    .Invoke(null, [finalQueryable])!;
            }
            else
            {
                return set?.Provider.Execute(finalExpression);
            }
        }


    }
}
