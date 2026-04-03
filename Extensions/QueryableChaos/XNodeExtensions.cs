using Extensions.Utilities;
using Extensions.Utilities.Extensions;
using Extensions.PrometheusTypes;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static class XNodeExtensions
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



    }
}
