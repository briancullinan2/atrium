using DataLayer.Entities;
using DataLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;


namespace Extensions.QueryableChaos
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
            var cleanExpression = new ClosureEvaluatorVisitor().Visit(expression);
            return new XDocument(VisitToXml(cleanExpression, 0, 0));
        }

        public static XElement VisitToXml(object? node, int currentDepth, int expressionDepth)
        {
            if (node == null) return new XElement("Null");

            var type = node.GetType();
            var element = new XElement(type.Name.ToSafe(),
                new XAttribute(nameof(Type.AssemblyQualifiedName), type.AssemblyQualifiedName ?? ""));

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


                if (prop.Name == nameof(TypeInfo.ImplementedInterfaces)
                    || prop.Name == nameof(TypeInfo.DeclaredProperties)
                    || prop.Name == nameof(Entity<>.QueryManager)
                    || prop.Name == nameof(TypeInfo.DeclaredMethods)
                    || prop.Name == nameof(Expression.CanReduce)
                    || prop.Name == nameof(LambdaExpression.TailCall))
                    continue;

                try
                {
                    var value = prop.GetValue(node);

                    /*
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
                    */

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
                                generics.Add(new XElement("Type", new XAttribute(nameof(Type.AssemblyQualifiedName), arg.AssemblyQualifiedName ?? "")));
                            }
                            methodInfo.Add(generics);
                        }
                        // Optional: Serialize the parameter types to be 100% sure on overloads
                        var paramsEl = new XElement("Parameters");
                        foreach (var p in parameters)
                        {
                            paramsEl.Add(new XElement("Parameter",
                                new XAttribute(nameof(Type.AssemblyQualifiedName), p.ParameterType.AssemblyQualifiedName ?? "")));
                        }
                        methodInfo.Add(paramsEl);
                        element.Add(propElement);
                    }
                    else if (value is ValueBuffer buffer)
                    {
                        var propElement = new XElement(prop.Name,
                            new XAttribute(nameof(ValueBuffer.Count), buffer.Count
                        ));
                        for (var i = 0; i < buffer.Count; i++)
                        {
                            var item = buffer[0];

                            if (item == null)
                            {
                                propElement.Add(new XElement("Null", new XAttribute("Value", string.Empty)));
                            }
                            else if (item.GetType().IsIterable() || !item.GetType().IsSimple())
                            {
                                propElement.Add(VisitToXml(item, currentDepth, expressionDepth + 1));
                            }
                            else
                            {
                                propElement.Add(new XElement(item?.GetType().Name.ToSafe() ?? "Null",
                                    new XAttribute("Value", item?.ToString() ?? string.Empty),
                                    new XAttribute(nameof(Type.AssemblyQualifiedName), item?.GetType().AssemblyQualifiedName ?? "")
                                ));
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
                                generics.Add(new XElement("Type", new XAttribute(nameof(Type.AssemblyQualifiedName), arg.AssemblyQualifiedName ?? "")));
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
                        typeElement.Add(new XAttribute(nameof(Type.Name), t.Name));
                        typeElement.Add(new XAttribute(nameof(Type.FullName), t.FullName ?? ""));
                        typeElement.Add(new XAttribute(nameof(Type.Namespace), t.Namespace ?? ""));
                        typeElement.Add(new XAttribute(nameof(Type.AssemblyQualifiedName), t.AssemblyQualifiedName ?? ""));

                        // If it's a generic type (like List<Pack>), you might need the generic arguments
                        if (t.IsGenericType)
                        {
                            var generics = new XElement("GenericArguments");
                            foreach (var arg in t.GetGenericArguments())
                            {
                                // One level of recursion for the arg is usually safe and necessary
                                generics.Add(new XElement("Type", new XAttribute(nameof(Type.AssemblyQualifiedName), arg.AssemblyQualifiedName ?? "")));
                            }
                            typeElement.Add(generics);
                        }

                        element.Add(typeElement);
                    }
                    else if (value?.GetType().Extends(typeof(AsyncQueryable<>)) == true)
                    {
                        var propElement = new XElement(prop.Name);
                        propElement.Add(VisitToXml(value, currentDepth + 1, expressionDepth));
                        element.Add(propElement);
                    }
                    else if (value is IEnumerable list && value is not string)
                    {
                        var propElement = new XElement(prop.Name);
                        foreach (var item in list)
                        {
                            if (item == null)
                            {
                                propElement.Add(new XElement("Null", new XAttribute("Value", string.Empty)));
                            }
                            else if (item.GetType().IsIterable() || !item.GetType().IsSimple())
                            {
                                propElement.Add(VisitToXml(item, currentDepth, expressionDepth + 1));
                            }
                            else
                            {
                                propElement.Add(new XElement(item?.GetType().Name.ToSafe() ?? "Null",
                                    new XAttribute("Value", item?.ToString() ?? string.Empty),
                                    new XAttribute(nameof(Type.AssemblyQualifiedName), item?.GetType().AssemblyQualifiedName ?? "")
                                ));
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
                { ExpressionType.Convert, BuildUnary },
                { ExpressionType.MemberAccess, BuildProperty },
                { ExpressionType.Index, BuildIndex },
                { ExpressionType.Invoke, BuildInvocation },
                { ExpressionType.Conditional, BuildConditional },
                { ExpressionType.NewArrayInit, BuildNewArrayInit },

                // Binary Expressions
                { ExpressionType.Not, BuildUnary },
                { ExpressionType.OrElse, BuildLeftRight },
                { ExpressionType.AndAlso, BuildLeftRight },

                // Comparators
                { ExpressionType.Equal, BuildLeftRight },
                { ExpressionType.NotEqual, BuildLeftRight },
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
                { ExpressionType.Power, BuildLeftRight },

                // Bitwise
                { ExpressionType.And, BuildLeftRight },
                { ExpressionType.Or, BuildLeftRight },
                { ExpressionType.ExclusiveOr, BuildLeftRight },

                // Null Handling (Essential for your 'Atrium' entities)
                { ExpressionType.Coalesce, BuildLeftRight },
                { ExpressionType.TypeAs, BuildTypeTest },
                { ExpressionType.TypeIs, BuildTypeTest },
                { ExpressionType.TypeEqual, BuildTypeTest },

                // Comparisons (You already have most, but don't forget these)
                //{ ExpressionType.TypeIs, BuildLeftRight } // Used for "is" keyword checks


                //{ExpressionType.Extension, BuildExtension}
            };

        public delegate Expression? ExpressionFactory(XElement el, Func<XElement, Expression?> ToExpression);
        private static Expression BuildNewArrayInit(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Get the array type (e.g., System.Object[])
            var typeName = el.Element(nameof(NewArrayExpression.Type))?
                .Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                ?? el.Element(nameof(NewArrayExpression.Type))?.Value;
            var arrayType = Type.GetType(typeName!)
                            ?? throw new InvalidOperationException($"Array type not found: {typeName}");

            // 2. Get the element type (System.Object)
            var elementType = arrayType.GetElementType() ?? typeof(object);

            // 3. Resolve all the expressions inside the { ... }
            var expressions = el.Element(nameof(NewArrayExpression.Expressions))?.Elements()
                .Select(x => ToExpression(x)!)
                .ToList() ?? [];

            return Expression.NewArrayInit(elementType, expressions);
        }

        private static Expression BuildConditional(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var test = ToExpression(el.Element(nameof(ConditionalExpression.Test))?.Elements().First()!)!;
            var ifTrue = ToExpression(el.Element(nameof(ConditionalExpression.IfTrue))?.Elements().First()!)!;
            var ifFalse = ToExpression(el.Element(nameof(ConditionalExpression.IfFalse))?.Elements().First()!)!;

            return Expression.Condition(test, ifTrue, ifFalse);
        }


        private static Expression? BuildProperty(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var expressionEl = el.Element(nameof(MemberExpression.Expression))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing expression");
            var expression = ToExpression(expressionEl);

            // Get the actual Metadata node (Runtimepropertyinfo)
            var memberEl = el.Element(nameof(MemberExpression.Member))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing member node");

            // Pull the 'Name' attribute specifically!
            string memberName = memberEl.Attribute(nameof(MemberInfo.Name))?.Value
                ?? throw new InvalidOperationException("Member name attribute missing");

            PropertyInfo? propertyInfo = ResolveMetadata(typeof(PropertyInfo), memberName, memberEl) as PropertyInfo
                ?? throw new InvalidOperationException("Could not resolve method info on " + el);

            return Expression.MakeMemberAccess(expression, propertyInfo);
        }




        private static Expression? BuildTypeTest(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var rightEl = (el.Element(nameof(TypeBinaryExpression.TypeOperand)) ?? el.Element(nameof(UnaryExpression.Type)))?.Elements().FirstOrDefault();
            var leftEl = (el.Element(nameof(TypeBinaryExpression.Expression)) ?? el.Element(nameof(UnaryExpression.Operand)))?.Elements().FirstOrDefault();
            if (rightEl == null || leftEl == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }
            var rightOperand = ResolveMetadata(typeof(Type), rightEl.ToString(), rightEl) as Type;
            var leftOperand = ToExpression(leftEl);
            if (rightOperand == null || leftOperand == null)
            {
                throw new InvalidOperationException("Could not resolve right expression on " + el);
            }

            var nodeType = el.Attribute(nameof(TypeBinaryExpression.NodeType))?.Value;

            return nodeType switch
            {
                nameof(ExpressionType.TypeIs) => Expression.TypeIs(leftOperand, rightOperand),
                nameof(ExpressionType.TypeAs) => Expression.TypeAs(leftOperand, rightOperand),
                nameof(ExpressionType.TypeEqual) => Expression.TypeEqual(leftOperand, rightOperand),
                _ => throw new InvalidOperationException($"Node Type '{nodeType}' not supported on host.")
            };

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

            var nodeType = el.Attribute(nameof(BinaryExpression.NodeType))?.Value;

            return nodeType switch
            {
                nameof(ExpressionType.Equal) => Expression.Equal(leftOperand, rightOperand),
                nameof(ExpressionType.NotEqual) => Expression.NotEqual(leftOperand, rightOperand),
                nameof(ExpressionType.AndAlso) => Expression.AndAlso(leftOperand, rightOperand),
                nameof(ExpressionType.OrElse) => Expression.OrElse(leftOperand, rightOperand),
                nameof(ExpressionType.LessThan) => Expression.LessThan(leftOperand, rightOperand),
                nameof(ExpressionType.LessThanOrEqual) => Expression.LessThanOrEqual(leftOperand, rightOperand),
                nameof(ExpressionType.GreaterThan) => Expression.GreaterThan(leftOperand, rightOperand),
                nameof(ExpressionType.GreaterThanOrEqual) => Expression.GreaterThanOrEqual(leftOperand, rightOperand),

                // The New Arithmetic Peers
                nameof(ExpressionType.Add) => Expression.Add(leftOperand, rightOperand),
                nameof(ExpressionType.Subtract) => Expression.Subtract(leftOperand, rightOperand),
                nameof(ExpressionType.Multiply) => Expression.Multiply(leftOperand, rightOperand),
                nameof(ExpressionType.Divide) => Expression.Divide(leftOperand, rightOperand),
                nameof(ExpressionType.Modulo) => Expression.Modulo(leftOperand, rightOperand),
                nameof(ExpressionType.Power) => Expression.Power(leftOperand, rightOperand),


                // The Bitwise & Null-Safety Peers
                nameof(ExpressionType.And) => Expression.And(leftOperand, rightOperand),
                nameof(ExpressionType.Or) => Expression.Or(leftOperand, rightOperand),
                nameof(ExpressionType.Coalesce) => Expression.Coalesce(leftOperand, rightOperand),
                nameof(ExpressionType.ExclusiveOr) => Expression.ExclusiveOr(leftOperand, rightOperand),

                _ => throw new InvalidOperationException($"Node Type '{nodeType}' not supported on host.")
            };
        }


        private static Expression? BuildLambda(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var parametersEl = el.Element(nameof(LambdaExpression.Parameters))?.Elements();
            var bodyEl = el.Element(nameof(LambdaExpression.Body))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing body");

            var parameters = parametersEl?.Select(p => ToExpression(p)).Cast<ParameterExpression>().ToList() ?? [];
            var realBody = ToExpression(bodyEl)
                ?? throw new InvalidOperationException("Body resolution failed");

            // 1. Get the intended Delegate Type (e.g., Func<Role, IEnumerable<Group>>)
            var typeName = el.Element(nameof(LambdaExpression.Type))?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                          ?? el.Element(nameof(LambdaExpression.Type))?.Value;

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
            var operandEl = el.Element(nameof(UnaryExpression.Operand))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException($"Missing operand: {el}");

            var operand = ToExpression(operandEl)
                ?? throw new InvalidOperationException("Operand resolution failed.");

            var nodeType = el.Attribute(nameof(UnaryExpression.NodeType))?.Value;

            if (nodeType == nameof(ExpressionType.Not)) return Expression.Not(operand);

            if (nodeType == nameof(ExpressionType.Quote)) return Expression.Quote(operand);

            if (nodeType == nameof(ExpressionType.Convert))
            {
                // 1. Prioritize AssemblyQualifiedName for Generics/Nullables
                var typeEl = el.Element(nameof(UnaryExpression.Type));
                var typeName = typeEl?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                              ?? typeEl?.Attribute(nameof(Type.FullName))?.Value
                              ?? throw new InvalidOperationException("Missing type metadata.");

                var resolvedType = Type.GetType(typeName);

                // 2. Fallback for common types if reflection is being finicky
                if (resolvedType == null && typeName.Contains(nameof(Nullable)))
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
            var instanceEl = el.Element(nameof(IndexExpression.Object))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("IndexExpression missing Object target.");
            var instance = ToExpression(instanceEl)!;

            // 2. Resolve the Indexer (PropertyInfo)
            var indexerEl = el.Element(nameof(IndexExpression.Indexer))?.Element("Runtimepropertyinfo")
                    ?? throw new InvalidOperationException("IndexExpression missing Indexer info.");
            var declaringTypeName = indexerEl.Element("DeclaringType")?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value;
            var declaringType = Type.GetType(declaringTypeName!)
                ?? throw new InvalidOperationException("IndexExpression missing declaring type.");

            var propertyName = indexerEl.Attribute(nameof(PropertyInfo.Name))?.Value ?? "Item";
            var reflectedTypeName = indexerEl.Element(nameof(PropertyInfo.ReflectedType))?
                .Attribute(nameof(Type.AssemblyQualifiedName))?.Value;
            var reflectedType = Type.GetType(reflectedTypeName!)
                ?? throw new InvalidOperationException($"Could not resolve ReflectedType: {reflectedTypeName}");

            // Use reflection to get the PropertyInfo for the indexer
            var propertyInfo = reflectedType.GetProperty(propertyName);

            // 3. Resolve the Arguments (the index values)
            var argumentsEl = el.Element(nameof(IndexExpression.Arguments))?.Elements() ?? [];
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
            var typeName = declEl?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value ?? declEl?.Value;

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
                            var expectedTypeStr = paramsEl[i].Attribute(nameof(Type.AssemblyQualifiedName))?.Value ?? throw new InvalidOperationException("Parameter type not know to disambiguate.");
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
                    var expectedTypeStr = e.Attribute(nameof(Type.AssemblyQualifiedName))?.Value;
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
                        .Select(g => Type.GetType(g.Attribute(nameof(Type.AssemblyQualifiedName))?.Value ?? g.Value))
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
            var methodEl = el.Element(nameof(MethodCallExpression.Method))?.Elements().FirstOrDefault()
                ?? throw new InvalidOperationException("Missing Method metadata");

            var methodInfo = ResolveMetadata(typeof(MethodInfo), null, methodEl) as MethodInfo
                ?? throw new InvalidOperationException("MethodInfo resolution failed");

            // 2. Resolve the Instance (The 'Object' attribute in your XML)
            // For LINQ extensions (static), this is usually null.
            var instanceEl = el.Element(nameof(MethodCallExpression.Object))?.Elements().FirstOrDefault();
            var instance = instanceEl != null ? ToExpression(instanceEl) : null;

            // 3. Resolve Arguments
            var args = el.Element(nameof(MethodCallExpression.Arguments))?.Elements()
                .Select(x =>
                {
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

                var values = el.Elements().Select(e =>
                {
                    if (e.Name == "Null") return null;
                    var typeName = e.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                        ?? throw new InvalidOperationException("Could not resolve type name on: " + e + " on " + el);
                    var entityType = Type.GetType(typeName)
                        ?? typeArgument
                        ?? throw new InvalidOperationException("Could not resolve type on: " + e + " on " + el);
                    if (e.Attribute("Value")?.Value == "Null")
                        return null;
                    else if (entityType.IsSimple())
                        return Convert.ChangeType(e.Attribute("Value")?.Value, entityType);
                    else
                        return ResolveMetadata(entityType, e.Attribute("Value")?.Value, e);
                }).ToArray();

                var castedValues = CollectionConverter.ConvertAsync(values, targetType);

                // Fallback for other IEnumerables
                return castedValues;
            }

            if (typeof(IEntity).IsAssignableFrom(targetType))
            {
                var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var entity = Activator.CreateInstance(targetType);
                foreach (var prop in props)
                {
                    if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                        continue;

                    if (prop.ReadOnly())
                        continue;

                    var baseType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    if ((el.Attribute(prop.Name)?.Value == "Null"
                        || el.Element(prop.Name)?.Attribute("Value")?.Value == "Null")
                        && prop.IsNullable())
                    {
                        prop.SetValue(entity, null);
                    }

                    else if (baseType.IsIterable()
                        && el.Element(prop.Name) is XElement complex)
                    {
                        var complexValue = ResolveMetadata(baseType, el.Attribute(prop.Name)?.Value, complex);
                        var convertedValue = CollectionConverter.ConvertAsync(complexValue, baseType);
                        if (convertedValue?.GetType().Extends(baseType) != true)
                            throw new InvalidOperationException("Iterable types don't match: " + complexValue?.GetType() + " -> " + convertedValue?.GetType() + " and " + baseType + " on " + el + " property " + prop);
                        prop.SetValue(entity, convertedValue);
                    }

                    else if (baseType.IsSimple())
                    {
                        var converter = TypeDescriptor.GetConverter(baseType);
                        object? simpleValue = null;
                        if (baseType == typeof(string))
                            simpleValue = el.Attribute(prop.Name)?.Value ?? string.Empty;
                        else if (!string.IsNullOrWhiteSpace(el.Attribute(prop.Name)?.Value))
                            simpleValue = converter.ConvertFromString(el.Attribute(prop.Name)!.Value);
                        if (!prop.IsNullable() && simpleValue?.GetType() != baseType)
                            throw new InvalidOperationException("Simple types don't match: " + simpleValue?.GetType() + " and " + baseType + " on " + el + " property " + prop);
                        prop.SetValue(entity, simpleValue);
                    }

                    else if (el.Element(prop.Name) is XElement complex2)
                    {
                        var complexValue = ResolveMetadata(baseType, el.Attribute(prop.Name)?.Value, complex2);
                        if (complexValue?.GetType() != baseType)
                            throw new InvalidOperationException("Complex types don't match: " + complexValue?.GetType() + " and " + baseType + " on " + el + " property " + prop);
                        prop.SetValue(entity, complexValue);
                    }

                    else
                        throw new InvalidOperationException("Don't know how to handle property type: " + baseType + " on " + el + " property " + prop
                            + " value is " + (el.Attribute(prop.Name)?.Value ?? el.Element(prop.Name)?.Attribute("Value")?.Value)
                            + " prop type is nullable " + prop.IsNullable());
                }
                return entity;
            }


            throw new InvalidOperationException("Don't know that type: " + targetType);
        }





        private static ConstantExpression BuildConstant(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var typeName = (el.Element(nameof(ConstantExpression.Type))?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value)
                ?? throw new InvalidOperationException("Could not resolve constant type attribute on: " + el);
            var type = Type.GetType(typeName)
                ?? throw new InvalidOperationException("Could not resolve constant type on: " + el);

            if (el.Element(nameof(ConstantExpression.Value)) is XElement complex)
            {
                // TODO: 
                if (complex.Element("Null") is not null)
                {
                    return Expression.Constant(null, type);
                }
                var val = ResolveMetadata(type, el.Attribute(nameof(ConstantExpression.Value))?.Value, type.IsIterable() ? complex : complex.Elements().FirstOrDefault() ?? complex);
                if (type.GetGenericArguments().FirstOrDefault() == typeof(Visit))
                {
                    Console.WriteLine("Made constant: " + JsonSerializer.Serialize(val) + el);
                }
                if (val?.GetType().IsSimple() == true
                    && val?.GetType() != type)
                {
                    return Expression.Constant(Convert.ChangeType(val, type), type);
                }
                if (type.IsIterable())
                {
                    val = CollectionConverter.ConvertAsync(val, type);
                }

                if (val?.GetType() != type)
                {
                    Console.WriteLine("ArgumentTypesMustMatch is dumbass error that doesn't tell me this type " + val?.GetType() + " or this type: " + type);
                }
                return Expression.Constant(val, type);
            }
            else if (el.Attribute(nameof(ConstantExpression.Value))?.Value is string val)
            {
                if (type == typeof(object) && val == "Null")
                {
                    return Expression.Constant(null, type);
                }
                else if (type.IsEnum)
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
        internal static readonly Dictionary<string, ParameterExpression> _parameters = [];
        private static ParameterExpression BuildParameter(XElement el, Func<XElement, Expression?> ToExpression)
        {
            // 1. Extract Name and Type accurately from the XML
            var name = el.Attribute(nameof(ParameterExpression.Name))?.Value ?? "x";
            var typeEl = el.Element(nameof(ParameterExpression.Type));
            var typeName = typeEl?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value
                          ?? typeEl?.Attribute(nameof(Type.FullName))?.Value
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
            var typeName = (el.Element(nameof(IQueryable.ElementType))?.Attribute(nameof(Type.AssemblyQualifiedName))?.Value)
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
            var typeStr = el.Attribute(nameof(Expression.NodeType))?.Value;
            if (typeStr == null || !_nodeTypeLookup.TryGetValue(typeStr, out var nodeType))
                return null;

            // Special cases: Parameters and Constants usually need manual handling 
            // because they don't follow the "Children as Expressions" rule perfectly.
            if (nodeType == ExpressionType.Extension)
            {
                var typeName = el.Element(nameof(IQueryable.ElementType))?
                    .Attribute(nameof(Type.AssemblyQualifiedName))?.Value;
                if (typeName?.Contains(nameof(DataLayer)) == true)
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
            if (el.Attribute(nameof(Type.AssemblyQualifiedName)) is XAttribute typeAttr
                && Type.GetType(typeAttr.Value) is Type targetType)
            {
                return Expression.Constant(ResolveMetadata(targetType, el.ToString(), el));
            }

            throw new NotSupportedException($"No factory found for {nodeType}");
        }



    }
}
