using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
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
        private static XElement VisitToXml(object node, int currentDepth, int expressionDepth)
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


            if (node is IEnumerable list2 && !(node is string))
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
                    else if (value is System.Collections.IEnumerable list && !(value is string))
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
        private static readonly Dictionary<string, ExpressionType> _nodeTypeLookup = Enum.GetValues(typeof(ExpressionType))
            .Cast<ExpressionType>()
            .ToDictionary(t => t.ToString(), t => t);
        private static readonly Dictionary<ExpressionType, List<Tuple<MethodInfo, List<ParameterInfo>>>> _factoryMap = typeof(Expression)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            // We filter for methods that take Expression arguments to avoid overloads like 'Constant'
            .Where(m => _nodeTypeLookup.ContainsKey(m.Name))
            .GroupBy(m => _nodeTypeLookup[m.Name])
            .ToDictionary(g => g.Key, g => g.Select(g => new Tuple<MethodInfo, List<ParameterInfo>>(g, g.GetParameters().ToList())).ToList());
        private static readonly Dictionary<ExpressionType, ExpressionFactory> _elementMap =
            new Dictionary<ExpressionType, ExpressionFactory>() {
                {ExpressionType.Call, BuildMethodCall},
                {ExpressionType.Parameter, BuildParameter},
                {ExpressionType.Constant, BuildConstant},
                {ExpressionType.Quote, BuildUnary},
                {ExpressionType.Lambda, BuildLambda},
                {ExpressionType.Equal, BuildEquals},
                {ExpressionType.Convert, BuildUnary},
                {ExpressionType.MemberAccess, BuildProperty},

                //{ExpressionType.Extension, BuildExtension}
            };

        public delegate Expression? ExpressionFactory(XElement el, Func<XElement, Expression?> ToExpression);

        private static Expression? BuildProperty(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var expressionEl = el.Element("Expression")?.Elements().FirstOrDefault();
            if (expressionEl == null)
            {
                throw new InvalidOperationException("Could not resolve property expression on " + el);
            }
            var expression = ToExpression(expressionEl);
            var memberInfo = el.Element("Member")?.Elements().FirstOrDefault();
            if (memberInfo == null)
            {
                throw new InvalidOperationException("Could not resolve property member on " + el);
            }
            PropertyInfo? propertyInfo = ResolveMetadata(typeof(PropertyInfo), memberInfo.Value, memberInfo) as PropertyInfo;
            if (propertyInfo == null)
            {
                throw new InvalidOperationException("Could not resolve method info on " + el);
            }

            return Expression.MakeMemberAccess(expression, propertyInfo);
        }


        private static Expression? BuildEquals(XElement el, Func<XElement, Expression?> ToExpression)
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
            return Expression.Equal(leftOperand, rightOperand);
        }

        private static Expression? BuildLambda(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var parametersEl = el.Element("Parameters")?.Elements();
            var bodyEl = el.Element("Body")?.Elements().FirstOrDefault();
            if (bodyEl == null)
            {
                throw new InvalidOperationException("Could not resolve lambda body on " + el);
            }
            IEnumerable<ParameterExpression>? parameters = parametersEl?.Select(p => ToExpression(p)).Cast<ParameterExpression>();
            var realBody = ToExpression(bodyEl);
            if (realBody == null)
            {
                throw new InvalidOperationException("Could not resolve lambda body on " + el);
            }
            return Expression.Lambda(realBody, parameters);
        }

        private static Expression? BuildUnary(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var argsEl = el.Element("Operand")?.Elements().FirstOrDefault();
            if (argsEl == null)
            {
                throw new InvalidOperationException("Could not resolve operand element on " + el);
            }
            var operand = ToExpression(argsEl);
            if (operand == null)
            {
                throw new InvalidOperationException("Could not resolve operand element on " + el);
            }
            if (el.Attribute("NodeType")?.Value == "Quote")
            {
                return Expression.Quote(operand);
            }
            else if (el.Attribute("NodeType")?.Value == "Convert")
            {
                var type = el.Element("Type")?.Attribute("FullName")?.Value;
                if (type == null)
                {
                    throw new InvalidOperationException("Could not resolve operand type on " + el);
                }
                var resolvedType = Type.GetType(type);
                if (resolvedType == null)
                {
                    throw new InvalidOperationException("Could not resolve operand type on " + el);
                }
                return Expression.Convert(operand, resolvedType);
            }
            else
            {
                throw new InvalidOperationException("Unary operation not implemented");
            }
        }

        private static Expression? BuildMethodCall(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var argsEl = el.Element("Arguments");
            var methodCall = _factoryMap[ExpressionType.Call].First(mi => mi.Item2.Count() == 1 + argsEl?.Elements().Count());
            var args = new List<object>();
            var methodEl = el.Element("Method");
            if (methodEl == null || argsEl == null)
            {
                throw new InvalidOperationException("Could not resolve method elements on " + el);
            }
            MethodInfo? methodInfo = ResolveMetadata(typeof(MethodInfo), methodEl.Value, methodEl.Elements().First()) as MethodInfo;
            if (methodInfo == null)
            {
                throw new InvalidOperationException("Could not resolve method info on " + el);
            }
            args.Add(methodInfo);
            for (var i = 0; i < argsEl.Elements().Count(); i++)
            {
                var argEl = argsEl.Elements().ElementAt(i);
                var requiredArg = ToExpression(argEl);
                if (requiredArg == null)
                {
                    throw new InvalidOperationException("Could not resolve expression argument on " + el);
                }
                args.Add(requiredArg);
            }
            return methodCall.Item1.Invoke(null, args.ToArray()) as Expression;
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

        private static object? ResolveMetadata(Type targetType, string? value, XElement el)
        {
            if (targetType == typeof(Type) && value != null)
                return Type.GetType(value);

            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(ExpressionType) && value != null)
                return Enum.Parse(typeof(ExpressionType), value);

            if (targetType == typeof(MethodInfo))
            {
                var declEl = el.Element("DeclaringType");
                var paramsCount = int.TryParse(el.Attribute("ParamsCount")?.Value, out var pc) ? pc : el.Element("Parameters")?.Elements().Count();
                var methodSourceType = declEl?.Attribute("AssemblyQualifiedName")?.Value ?? declEl?.Value;
                var methodName = el.Attribute("Name")?.Value;
                if (methodSourceType == null || methodName == null)
                {
                    throw new InvalidOperationException("Cannot find type name on " + el.ToString());
                }
                var declaringType = Type.GetType(methodSourceType);
                var method = declaringType?.GetMethods()
                    // TODO: fix arg count when i test Packs.CountAsync(p => p.Qualifier)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == (paramsCount ?? 0));
                if (method == null)
                {
                    throw new InvalidOperationException("Cannot find method on " + el.ToString());
                }
                if (method.ContainsGenericParameters)
                {
                    var genericsEl = el.Element("GenericArguments");
                    if (genericsEl == null)
                    {
                        throw new InvalidOperationException("Cannot resolve generic arguments on " + el.ToString());
                    }
                    var genericTypes = genericsEl.Elements()
                        .Select(el2 => Type.GetType(el2.Attribute("AssemblyQualifiedName")?.Value ?? ""))
                        .Cast<Type>().ToArray();
                    method = method.MakeGenericMethod(genericTypes);

                }
                return method;
            }

            if (targetType == typeof(PropertyInfo))
            {
                var declEl = el.Element("DeclaringType");
                var paramsCount = int.TryParse(el.Attribute("ParamsCount")?.Value, out var pc) ? pc : el.Element("Parameters")?.Elements().Count();
                var methodSourceType = declEl?.Attribute("AssemblyQualifiedName")?.Value ?? declEl?.Value;
                var methodName = el.Attribute("Name")?.Value;
                if (methodSourceType == null || methodName == null)
                {
                    throw new InvalidOperationException("Cannot find type name on " + el.ToString());
                }
                var declaringType = Type.GetType(methodSourceType);
                var property = declaringType?.GetProperties()
                    // TODO: fix arg count when i test Packs.CountAsync(p => p.Qualifier)
                    .FirstOrDefault(m => m.Name == methodName /*&& m.GetIndexParameters().Length == (paramsCount ?? 0)*/);
                if (property == null)
                {
                    throw new InvalidOperationException("Cannot find property on " + el.ToString());
                }
                /*
                TODO: ???
                if (method.ContainsGenericParameters)
                {
                    var genericsEl = el.Element("GenericArguments");
                    if (genericsEl == null)
                    {
                        throw new InvalidOperationException("Cannot resolve generic arguments on " + el.ToString());
                    }
                    var genericTypes = genericsEl.Elements()
                        .Select(el2 => Type.GetType(el2.Attribute("AssemblyQualifiedName")?.Value ?? ""))
                        .Cast<Type>().ToArray();
                    method = method.MakeGenericMethod(genericTypes);

                }
                */
                return property;

            }

            return null;
        }

        private static Expression? BuildExtension(XElement el, DbContext context, out IQueryable? set)
        {
            var typeName = el.Element("ElementType")?.Attribute("AssemblyQualifiedName")?.Value;
            if (typeName == null)
            {
                throw new InvalidOperationException("Could not resolve extension type on: " + el);
            }
            var entityType = Type.GetType(typeName);

            // Reflection Sparkle: Find the DbSet<T> property on TranslationContext 
            // where T is our entityType.
            var dbSetProperty = typeof(TranslationContext)
                .GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                     p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                                     p.PropertyType.GetGenericArguments()[0] == entityType);
            var asQueryableMethod = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == "AsQueryable" && m.IsGenericMethod)
                .MakeGenericMethod(entityType);

            if (dbSetProperty != null)
            {
                // Pull the live DbSet instance from your context
                set = asQueryableMethod.Invoke(null, new[] { dbSetProperty.GetValue(context) }) as IQueryable;
                // TODO: out DbSet until expression can 
                return set?.Expression; // This is the "Real" root for EF Core
            }

            throw new InvalidOperationException($"No DbSet found for {entityType?.Name} in TranslationContext.");
        }

        private static ConstantExpression BuildConstant(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var val = el.Attribute("Value")?.Value;
            var typeName = el.Element("Type")?.Attribute("FullName")?.Value;
            if (typeName == null)
            {
                throw new InvalidOperationException("Could not resolve constant type on: " + el);
            }
            var type = Type.GetType(typeName) ?? typeof(string);

            // Use Activator to convert the string value back to the target type
            var convertedValue = Convert.ChangeType(val, type);
            return Expression.Constant(convertedValue, type);
        }

        // Keep a cache of parameters during a single Reconstruction pass
        private static Dictionary<string, ParameterExpression> _parameters = new();

        private static ParameterExpression BuildParameter(XElement el, Func<XElement, Expression?> ToExpression)
        {
            var name = el.Element("Name")?.Value ?? "x";
            var typeName = el.Element("Type")?.Attribute("FullName")?.Value; // From your 'fluffy' reflection
            if (typeName == null)
            {
                throw new InvalidOperationException("Could not resolve parameter type on: " + el);
            }
            var type = Type.GetType(typeName) ?? typeof(object);

            // Important: Re-use the same ParameterExpression object for the same name
            if (!_parameters.TryGetValue(name, out var parameter))
            {
                parameter = Expression.Parameter(type, name);
                _parameters.Add(name, parameter);
            }
            return parameter;
        }

        public static string ToSerialized(IQueryable query)
        {
            return ToXDocument(query.Expression).ToString();
        }

        public static object? ToQueryable(string query, IServiceProvider _service)
        {
            using var scope = _service.CreateScope();
            var ephemeralStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
            using var context = ephemeralStore.CreateDbContext();


            using (XmlReader reader = XmlReader.Create(new StringReader(query)))
            {
                reader.MoveToContent();
                XElement root = (XElement)XNode.ReadFrom(reader);

                // 1. Clear the parameter cache for this specific run
                _parameters.Clear();

                // 2. Reconstruct the raw Expression tree
                // TODO: fix this, won't know how until i debug expressions and see what parts of the trees it can show in
                IQueryable? set;
                Expression? finalExpression = root.ToExpression(context, out set);
                if (finalExpression == null)
                {
                    throw new InvalidOperationException("Could not convert expression document to Queryable: " + query);
                }

                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(finalExpression.Type) && finalExpression.Type != typeof(string))
                {
                    // It's a sequence - force materialization to avoid SingleQueryingEnumerable leaks
                    var toListMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == "ToList" && m.IsGenericMethod)
                        .MakeGenericMethod(finalExpression.Type.GenericTypeArguments[0]);

                    return toListMethod.Invoke(null, new[] { set });
                }
                else
                {
                    return set.Provider.Execute(finalExpression);
                }

            }
        }


    }
}
