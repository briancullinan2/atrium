using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
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
        private static readonly int _maxExpressionDepth = 20;

        public static XDocument ToXDocument(this Expression expression)
        {
            return new XDocument(VisitToXml(expression, 0, 0));
        }

        public static XElement VisitToXml(object? node, int currentDepth, int expressionDepth)
        {
            if (node == null) return new XElement("Null");

            var type = node.GetType();
            var element = new XElement(type.Name.ToSafe(),
                new XAttribute("AssemblyQualifiedName", type.AssemblyQualifiedName ?? ""));

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

                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                if (prop.Name == "ImplementedInterfaces" || prop.Name == "DeclaredProperties"
                    || prop.Name == "QueryManager"
                    || prop.Name == "DeclaredMethods" || prop.Name == "CanReduce" || prop.Name == "TailCall") continue;

                try
                {
                    var value = prop.GetValue(node);

                    if (value is MemberExpression memberExpression
                        && memberExpression.Expression is ConstantExpression constant 
                        && constant.Value?.GetType().Name.Contains("<>c__DisplayClass") == true)
                    {
                        // Access the field on the specific instance of the closure
                        //var field = (FieldInfo)memberExpression.Member;
                        //var actualValue = field.GetValue(constant.Value);
                        //Console.WriteLine("DisplayClass: " + constant.Value);
                        var lambda = Expression.Lambda(memberExpression);
                        var result = lambda.Compile().DynamicInvoke();
                        //Console.WriteLine(result);
                        var propElement = new XElement(prop.Name);
                        propElement.Add(VisitToXml(Expression.Constant(result), currentDepth, expressionDepth + 1));
                        element.Add(propElement);
                    }

                    // Treat Expression, MethodInfo, and Type as "Complex" to recurse into them
                    /*else if (value is ConstantExpression constant2 
                        && constant2.Value?.GetType().Name.Contains("<>c__DisplayClass") == true
                        // && constant.Value is ISerializable
                    ) {
                        Console.WriteLine("DisplayClass: " + constant2.Value);
                        var lambda = Expression.Lambda(constant2);
                        var result = lambda.Compile().DynamicInvoke();
                        Console.WriteLine(result);
                        var propElement = new XElement(prop.Name);
                        propElement.Add(VisitToXml(Expression.Constant(result), currentDepth, expressionDepth + 1));
                        element.Add(propElement);
                    }*/
                    else if (value is Expression)
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
                                new XAttribute("AssemblyQualifiedName", p.ParameterType.AssemblyQualifiedName ?? "")));
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
                                    new XAttribute("AssemblyQualifiedName", item?.GetType().AssemblyQualifiedName ?? "")
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
                            if (item == null || item.GetType().IsSimple() == true)
                            {
                                propElement.Add(new XElement(item?.GetType().Name.ToSafe() ?? "Null",
                                    new XAttribute("Value", item?.ToString() ?? string.Empty),
                                    new XAttribute("AssemblyQualifiedName", item?.GetType().AssemblyQualifiedName ?? "")
                                ));
                            }
                            else
                            {
                                propElement.Add(VisitToXml(item, currentDepth, expressionDepth + 1));
                            }
                        }
                        element.Add(propElement);

                    }
                    else if (value == null || value.GetType().IsSimple() == true)
                    {
                        element.Add(new XAttribute(prop.Name, value?.ToString() ?? "Null"));
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
                { ExpressionType.NotEqual, BuildLeftRight },
                { ExpressionType.Convert, BuildUnary },
                { ExpressionType.MemberAccess, BuildProperty },
                { ExpressionType.OrElse, BuildLeftRight },
                { ExpressionType.Index, BuildIndex },
                { ExpressionType.Invoke, BuildInvocation },
                { ExpressionType.Conditional, BuildConditional },
                { ExpressionType.NewArrayInit, BuildNewArrayInit },
                { ExpressionType.AndAlso, BuildLeftRight },
                { ExpressionType.LessThan, BuildLeftRight },
                { ExpressionType.LessThanOrEqual, BuildLeftRight },
                { ExpressionType.GreaterThan, BuildLeftRight },
                { ExpressionType.GreaterThanOrEqual, BuildLeftRight },
                
                // Arithmetic
                { ExpressionType.Add, BuildLeftRight },
                { ExpressionType.Subtract, BuildLeftRight },
                { ExpressionType.Multiply, BuildLeftRight },
                { ExpressionType.Divide, BuildLeftRight },
                { ExpressionType.Modulo, BuildLeftRight },

                // Bitwise
                { ExpressionType.And, BuildLeftRight },
                { ExpressionType.Or, BuildLeftRight },
                { ExpressionType.ExclusiveOr, BuildLeftRight },

                // Null Handling (Essential for your 'Atrium' entities)
                { ExpressionType.Coalesce, BuildLeftRight },

                // Comparisons (You already have most, but don't forget these)
                //{ ExpressionType.TypeIs, BuildLeftRight } // Used for "is" keyword checks


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
            var expressionEl = el.Element("Expression")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing expression");
            var expression = ToExpression(expressionEl);

            // Get the actual Metadata node (Runtimepropertyinfo)
            var memberEl = el.Element("Member")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing member node");

            // Pull the 'Name' attribute specifically!
            string memberName = memberEl.Attribute("Name")?.Value
                ?? throw new InvalidOperationException("Member name attribute missing");

            PropertyInfo? propertyInfo = ResolveMetadata(typeof(PropertyInfo), memberName, memberEl) as PropertyInfo 
                ?? throw new InvalidOperationException("Could not resolve method info on " + el);

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
            
            var nodeType = el.Attribute("NodeType")?.Value;

            return nodeType switch
            {
                "Equal" => Expression.Equal(leftOperand, rightOperand),
                "NotEqual" => Expression.NotEqual(leftOperand, rightOperand),
                "AndAlso" => Expression.AndAlso(leftOperand, rightOperand),
                "OrElse" => Expression.OrElse(leftOperand, rightOperand),
                "LessThan" => Expression.LessThan(leftOperand, rightOperand),
                "LessThanOrEqual" => Expression.LessThanOrEqual(leftOperand, rightOperand),
                "GreaterThan" => Expression.GreaterThan(leftOperand, rightOperand),
                "GreaterThanOrEqual" => Expression.GreaterThanOrEqual(leftOperand, rightOperand),

                // The New Arithmetic Peers
                "Add" => Expression.Add(leftOperand, rightOperand),
                "Subtract" => Expression.Subtract(leftOperand, rightOperand),
                "Multiply" => Expression.Multiply(leftOperand, rightOperand),
                "Divide" => Expression.Divide(leftOperand, rightOperand),
                "Modulo" => Expression.Modulo(leftOperand, rightOperand),

                // The Bitwise & Null-Safety Peers
                "And" => Expression.And(leftOperand, rightOperand),
                "Or" => Expression.Or(leftOperand, rightOperand),
                "Coalesce" => Expression.Coalesce(leftOperand, rightOperand),
                "ExclusiveOr" => Expression.ExclusiveOr(leftOperand, rightOperand),

                _ => throw new InvalidOperationException($"Node Type '{nodeType}' not supported on host.")
            };
        }


        private static Expression? BuildLambda(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var parametersEl = el.Element("Parameters")?.Elements();
            var bodyEl = el.Element("Body")?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing body");

            var parameters = parametersEl?.Select(p => ToExpression(p)).Cast<ParameterExpression>().ToList() ?? [];
            var realBody = ToExpression(bodyEl)
                ?? throw new InvalidOperationException("Body resolution failed");

            // 1. Get the intended Delegate Type (e.g., Func<Role, IEnumerable<Group>>)
            var typeName = el.Element("Type")?.Attribute("AssemblyQualifiedName")?.Value
                          ?? el.Element("Type")?.Value;

            if (!string.IsNullOrEmpty(typeName))
            {
                var delegateType = Type.GetType(typeName);
                if (delegateType != null)
                {
                    // 2. Force the lambda to implement this specific delegate
                    return Expression.Lambda(delegateType, realBody, parameters);
                }
            }

            // Fallback to inference if Type metadata is missing
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
            // SHIM: Turn EF internal markers back into public extensions
            if (methodName == "NotQuiteInclude")
            {
                methodName = "Include";
            }
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
                var properties = declaringType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(m => m.Name == methodName && (paramsCount == 0 || m.GetIndexParameters().Length == paramsCount))
                    .ToList();

                var paramsEl = el.Element("Parameters")?.Elements().ToList();
                if (properties.Count > 1 && paramsEl != null)
                {
                    // reduce by parameter type matching
                    properties = [.. properties.Where(m =>
                    {
                        var methodParams = m.GetIndexParameters();
                        if (methodParams.Length != paramsEl.Count) return false;

                        for (int i = 0; i < methodParams.Length; i++)
                        {
                            var expectedTypeStr = paramsEl[i].Attribute("AssemblyQualifiedName")?.Value ?? throw new InvalidOperationException("Parameter type not know to disambiguate.");
                            var expectedType = Type.GetType(expectedTypeStr) ?? throw new InvalidOperationException("Parameter not know to disambiguate.");

                            // Get the actual parameter type name (handles generics like TSource)
                            var actualParamType = methodParams[i].ParameterType;

                            // We check if the XML's type string 'contains' the ParameterType name.
                            // This handles the discrepancy between "System.Linq.IQueryable`1" 
                            // and the fully qualified version in your XML.
                            if (actualParamType != expectedType)
                                return false;
                        }
                        return true;
                    })];
                }

                if (properties.Count == 0)
                    throw new InvalidOperationException($"Method {methodName} with {paramsCount} params not found on {typeName}");

                return properties.First() as T;
            }
            else if (typeof(MethodInfo).IsAssignableFrom(typeof(T)))
            {

                // 3. Smart Method Selection
                // We filter by name and count, but we prioritize Generic Method Definitions
                // because that's what Select/Where/FirstOrDefault are.
                var methods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(m => m.Name == methodName && m.GetParameters().Length == paramsCount)
                    .ToList();

                // If there's an ambiguity (e.g., Select vs Select with index), 
                // the XML's GenericArguments will tell us if we're looking for the generic one.
                var paramsEl = el.Element("Parameters")?.Elements().ToList();
                var expectedTypeParams = paramsEl?.Select(e =>
                {
                    var expectedTypeStr = e.Attribute("AssemblyQualifiedName")?.Value;
                    var expectedType = Type.GetType(expectedTypeStr!);
                    return expectedType;
                });


                if (methods.Count > 1 && paramsEl != null && paramsEl.Count > 0)
                {
                    // reduce by parameter type matching
                    methods = [.. methods.Where(m =>
                    {
                        var methodParams = m.GetParameters();
                        if (methodParams.Length != paramsEl.Count) return false;

                        for (int i = 0; i < methodParams.Length; i++)
                        {
                            var actualParamType = methodParams[i].ParameterType;
                            var expectedType = expectedTypeParams?.ElementAtOrDefault(i);
                            if (expectedType == null) return false;

                            // HARD FILTER: If both are concrete, they must be assignable
                            if (!actualParamType.IsGenericParameter && !actualParamType.ContainsGenericParameters)
                            {
                                if (!actualParamType.IsAssignableFrom(expectedType)) return false;
                            }
        
                            // HARD FILTER: Generic Arity Check (The "Select Index" Killer)
                            // If both are Generic Types (e.g., Func<..>), they must have the same number of T arguments
                            if (actualParamType.IsGenericType && expectedType.IsGenericType)
                            {
                                if (actualParamType.GetGenericArguments().Length != expectedType.GetGenericArguments().Length)
                                    return false;
                            }
                        }
                        return true;
                    })];
                }


                if (methods.Count > 1 && paramsEl != null && paramsEl.Count > 0)
                {
                    methods = [..methods.OrderByDescending(m =>
                    {
                        var methodParams = m.GetParameters();
                        int score = 0;

                        for (int i = 0; i < methodParams.Length; i++)
                        {
                            var expectedType = expectedTypeParams?.ElementAtOrDefault(i);
                            var actualParamType = methodParams[i].ParameterType;

                            // Perfect match (usually for non-generic params like 'int' or 'string')
                            if (actualParamType == expectedType) score += 1000;

                            // Generic Definition Match (e.g., both are Expression<TDelegate>)
                            if (actualParamType.IsGenericType && expectedType!.IsGenericType &&
                                actualParamType.GetGenericTypeDefinition() == expectedType.GetGenericTypeDefinition())
                            {
                                score += 500;
                            }

                            // Name match is a good tie-breaker for IQueryable vs IEnumerable
                            if (actualParamType.Name == expectedType?.Name) score += 100;
                        }
                        return score;
                    })];
                }

                if (methods.Count == 0)
                    throw new InvalidOperationException($"Method {methodName} with {paramsCount} params not found on {typeName}");


                MethodInfo method;
                var genericsEl = el.Element("GenericArguments");
                if (genericsEl != null && genericsEl.HasElements)
                {
                    method = methods.FirstOrDefault(m => m.IsGenericMethodDefinition)
                        ?? methods.FirstOrDefault() ?? throw new InvalidOperationException("Could not resolve method: " + el);

                    var genericTypes = genericsEl.Elements()
                        .Select(g => Type.GetType(g.Attribute("AssemblyQualifiedName")?.Value ?? g.Value))
                        .Cast<Type>()
                        .ToArray();

                    method = method.MakeGenericMethod(genericTypes);
                }
                else
                {
                    method = methods.FirstOrDefault() ?? throw new InvalidOperationException("Could not resolve method: " + el);
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
                .Select(x => {
                    var expr = ToExpression(x)!;

                    //if (expr is LambdaExpression)
                    //{
                        // EF Core needs the 'Quote' to treat the lambda as data, not code
                    //    return Expression.Quote(expr);
                    //}
                    // UNWRAP QUOTES: If the node is a Quote, we often need the 
                    // underlying Lambda for the method call to validate types correctly.
                    if (expr is UnaryExpression unary && expr.NodeType == ExpressionType.Quote)
                    {
                        return unary.Operand;
                    }

                    return expr;
                })
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


            if (targetType == typeof(ValueBuffer)
                || (typeof(IEnumerable).IsAssignableFrom(targetType) 
                    && targetType != typeof(string)))
            {
                var typeArgument = targetType.GetGenericArguments().FirstOrDefault();

                var values = el.Elements().Select(e => {
                    if (e.Name == "Null") return null;
                    var typeName = e.Attribute("AssemblyQualifiedName")?.Value
                        ?? throw new InvalidOperationException("Could not resolve type name on: " + e);
                    var entityType = Type.GetType(typeName)
                        ?? typeArgument
                        ?? throw new InvalidOperationException("Could not resolve type on: " + e);
                    if (e.Attribute("Value")?.Value == "Null")
                        return null;
                    else if (entityType.IsSimple())
                        return Convert.ChangeType(e.Attribute("Value")?.Value, entityType);
                    else 
                        return ResolveMetadata(entityType, e.Attribute("Value")?.Value, e);
                }).ToArray();

                if (targetType == typeof(ValueBuffer))
                    return new ValueBuffer(values);

                var itemType = targetType.IsArray
                    ? targetType.GetElementType() ?? typeof(object)
                    : targetType.GetGenericArguments().FirstOrDefault() ?? typeof(object);

                // 1. Convert our 'values' (which is likely List<object>) to IEnumerable<TItem>
                var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))
                    ?.MakeGenericMethod(itemType)
                    ?? throw new InvalidOperationException("Cast failed");

                var castedValues = castMethod.Invoke(null, [values]);

                // 2. Handle Arrays
                if (targetType.IsArray)
                {
                    var toArrayMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))
                        ?.MakeGenericMethod(itemType);
                    return toArrayMethod!.Invoke(null, [castedValues]);
                }

                // 3. Handle Lists (List<T>)
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var toListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))
                        ?.MakeGenericMethod(itemType);
                    return toListMethod!.Invoke(null, [castedValues]);
                }

                // Fallback for other IEnumerables
                return castedValues;
            }

            if(typeof(IEntity).IsAssignableFrom(targetType))
            {
                var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var entity = Activator.CreateInstance(targetType);
                foreach(var prop in props)
                {
                    if(prop.PropertyType.IsSimple())
                    {
                        var converter = TypeDescriptor.GetConverter(prop.PropertyType);
                        var simpleValue = converter.ConvertFromString(el.Attribute(prop.Name)?.Value ?? string.Empty);
                        prop.SetValue(entity, simpleValue);
                    }
                    else if (el.Element(prop.Name) is XElement complex)
                    {
                        var complexValue = ResolveMetadata(prop.PropertyType, el.Attribute(prop.Name)?.Value, complex);
                        prop.SetValue(entity, complexValue);
                    }
                }
                return entity;
            }


            throw new InvalidOperationException("Don't know that type: " + targetType);
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
                if(complex.Element("Null") is not null)
                {
                    return Expression.Constant(null, type);
                }
                var val = ResolveMetadata(type, el.Attribute("Value")?.Value, complex);
                if(val?.GetType().IsSimple() == true
                    && val?.GetType() != type)
                {
                    return Expression.Constant(Convert.ChangeType(val, type), type);
                }
                
                if (val?.GetType() != type)
                {
                    Console.WriteLine("ArgumentTypesMustMatch is dumbass error that doesn't tell me this type " + val?.GetType() + " or this type: " + type);
                }
                return Expression.Constant(val, type);
            } 
            else if (el.Attribute("Value")?.Value is string val)
            {
                if(type == typeof(object) && val == "Null")
                {
                    return Expression.Constant(null, type);
                }
                else if(type.IsEnum)
                {
                    return Expression.Constant(val.TryParse(type), type);
                }
                else if (type.IsSimple() && val.GetType() != type)
                {
                    return Expression.Constant(Convert.ChangeType(val, type), type);
                }
                else
                {
                    return Expression.Constant(val, type);
                }
            }
            throw new InvalidOperationException("Cannot extract constant value.");
        }

        // Keep a cache of parameters during a single Reconstruction pass
        private static readonly Dictionary<string, ParameterExpression> _parameters = [];
        private static ParameterExpression BuildParameter(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Extract Name and Type accurately from the XML
            var name = el.Attribute("Name")?.Value ?? "x";
            var typeEl = el.Element("Type");
            var typeName = typeEl?.Attribute("AssemblyQualifiedName")?.Value
                          ?? typeEl?.Attribute("FullName")?.Value
                          ?? throw new InvalidOperationException("Could not resolve parameter type");

            var type = Type.GetType(typeName) ?? typeof(object);

            // 2. Create a unique key using BOTH Name and Type
            var key = $"{name}_{type.FullName}";

            if (!_parameters.TryGetValue(key, out var parameter))
            {
                parameter = Expression.Parameter(type, name);
                _parameters.Add(key, parameter);
            }
            return parameter;
        }



        private static Expression? BuildExtension(XElement el, DbContext context, out IQueryable? set)
        {
            var typeName = (el.Element("ElementType")?.Attribute("AssemblyQualifiedName")?.Value)
                ?? throw new InvalidOperationException("Could not resolve extension type on: " + el);

            var entityType = Type.GetType(typeName)
                ?? throw new InvalidOperationException("Could not resolve type on: " + el);

            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), [])
                    ?.MakeGenericMethod(entityType)
                    ?? throw new InvalidOperationException("Could not render set creator in context: " + el);
            set = setMethod.Invoke(context, []) as IQueryable;
            return set?.Expression;
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
            if(el.Attribute("AssemblyQualifiedName") is XAttribute typeAttr
                && Type.GetType(typeAttr.Value) is Type targetType)
            {
                return Expression.Constant(ResolveMetadata(targetType, el.ToString(), el));
            }

            throw new NotSupportedException($"No factory found for {nodeType}");
        }



        public static string ToSerialized(IQueryable query)
        {
            return ToXDocument(query.Expression).ToString();
        }

        public static async Task<object?> ToQueryable(string query, DbContext context)
        {
            // 1. Clear the parameter cache for this specific run
            _parameters.Clear();

            using XmlReader reader = XmlReader.Create(new StringReader(query));
            _ = reader.MoveToContent();
            XElement root = (XElement)XNode.ReadFrom(reader);

            // TODO: fix this, won't know how until i debug expressions and see what parts of the trees it can show in
            Expression? finalExpression = ToExpression(root, context, out IQueryable ? set)

                ?? throw new InvalidOperationException("Could not convert expression document to Queryable: " + query);
            if (typeof(IEnumerable).IsAssignableFrom(finalExpression.Type)
                && !finalExpression.Type.IsArray
                && finalExpression.Type != typeof(string))
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
            else if (set?.Provider is IAsyncQueryProvider asyncProvider)
            {
                return await asyncProvider.ExecuteAsync<Task<object?>>(finalExpression);
            }
            else
            {
                return set?.Provider.Execute(finalExpression);
            }


        }


    }
}
