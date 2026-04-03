using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Extensions.QueryableChaos
{
    public static partial class LinqExtensions
    {


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


    }
}
