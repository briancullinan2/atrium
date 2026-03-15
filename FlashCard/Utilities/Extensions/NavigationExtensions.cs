using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;
using System.Reflection;

namespace FlashCard.Utilities.Extensions
{
    public static class NavigationExtensions
    {

        // TODO: i thought there was some fancy c# pattern that has the object expression as the coalescing parameters that makes an object of type <T>?
        /*
        public static string GetUri<TComponent>(Expression<NewExpression>? initializer = null) where TComponent : IComponent
        {
            return GetUri<TComponent>(initializer as Expression<Func<object>>);
        }
        public static string GetUri<TComponent, TObject>(Expression<Func<TObject>>? initializer = null) where TComponent : IComponent where TObject : TComponent
        {
            return GetUri<TComponent>(initializer as Expression<Func<object>>);
        }
        */

        static NavigationExtensions()
        {
            var routeableTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.GetCustomAttributes<RouteAttribute>().Any());

            foreach (var type in routeableTypes)
            {
                // This triggers your existing GetRoutes logic to fill the _routeCache
                _ = GetRoutes(type);
            }
        }

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
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
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

        // TODO: generic implicit ?
        // public static implicit operator RouteData<T>(T source) => new RouteData<T>();

        //public static string GetUri<TComponent>(Expression<Func<TComponent, object?>>? initializer) where TComponent : IComponent
        //{
        //    var values = initializer?.ToDictionary();
        //    return GetUri<TComponent>(values);
        //}
        //public static string GetUri<TComponent>(this NavigationManager Nav, Expression<Func<TComponent, TComponent>> initializer) where TComponent : IComponent
        //{
        //    var values = initializer.ToDictionary();
        //    return Nav.GetUriWithQueryParameters(values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as object));
        //}


        public static void NavigateTo<TComponent>(this NavigationManager Nav, Expression<Func<TComponent, TComponent>>? initializer = null) where TComponent : IComponent, new()
        {
            if (initializer == null)
            {
                Nav.NavigateTo(GetUri((TComponent c) => new TComponent() { }));
                return;
            }
            Nav.NavigateTo(GetUri(initializer));
        }


        public static string GetUri<TComponent>(Expression<Func<TComponent, TComponent>> initializer) where TComponent : IComponent
        {
            var values = initializer.ToDictionary();
            return GetUri<TComponent>(values);
        }

        // NOT TYPESAFE
        //public static string GetUri<TComponent>(Expression<Func<TComponent>> initializer) where TComponent : IComponent
        //{
        //    var values = initializer?.ToDictionary();
        //    return GetUri<TComponent>(values);
        //}

        //public static string GetUri<TComponent>(Expression<Func<object>> initializer) where TComponent : IComponent
        //{
        //    var values = initializer?.ToDictionary();
        //    return GetUri<TComponent>(values);
        //}

        public static string GetUri<TComponent>(Dictionary<string, string?>? initializer = null) where TComponent : IComponent
        {
            var componentType = typeof(TComponent);
            return GetUri(componentType, initializer);
        }

        private class ParsedRoute
        {
            internal string? Template;
            internal IEnumerable<string>? Segments;
            internal IEnumerable<string>? Params;
            internal bool Wildcard;
        }
        private static Dictionary<Type, IEnumerable<ParsedRoute>> _routeCache = new();

        private static IEnumerable<ParsedRoute> GetRoutes(Type componentType)
        {
            // not sure how this would get in there?
            if (_routeCache.ContainsKey(componentType))
            {
                if (_routeCache[componentType].Count() == 0)
                {
                    throw new ArgumentException($"Type {componentType.Name} does not have any [RouteAttribute] defined.");
                }

                return _routeCache[componentType];
            }

            var routes = componentType.GetCustomAttributes<RouteAttribute>().Select(attr => attr.Template).ToList();
            if (routes == null || routes.Count == 0)
            {
                throw new ArgumentException($"Type {componentType.Name} does not have any [RouteAttribute] defined.");
            }

            var parameterMatcher = @"\{([^:{}]+)(.*?)?\}|(\*)";
            _routeCache[componentType] = routes
                .Select(r => new
                {
                    Template = r,
                    Segments = r.Split("/"),
                    Params = System.Text.RegularExpressions.Regex.Matches(r, parameterMatcher)
                                .Select(m => !string.IsNullOrEmpty(m.Groups[1].Value)
                                      ? m.Groups[1].Value  // Returns "id" or "*path"
                                      : m.Groups[3].Value).ToList()
                })
                .Select(m => new ParsedRoute
                {
                    Template = m.Template,
                    Segments = m.Segments,
                    Params = m.Params,
                    Wildcard = m.Params.Any(p => p.StartsWith("*")),
                }).ToList();

            return _routeCache[componentType];

        }


        public static string GetUri(Type componentType, Dictionary<string, string?>? initializer = null)
        {

            var sortedRoutes = GetRoutes(componentType);

            // 2. Select the "Best" route
            // We want the route that has the same number of parameters as our 'values' count
            // Or the one with the highest match count that doesn't exceed our values.
            var matchedRoutes = sortedRoutes
                .Select(m => new
                {
                    m.Wildcard,
                    m.Params,
                    m.Template,
                    Matches = m.Params?.Count(p => initializer?.ContainsKey(p) == true)
                            + m.Segments?.Count(s => initializer?.Any(kvp => s.Equals(kvp.Value, StringComparison.InvariantCultureIgnoreCase)) == true)
                })
                .OrderByDescending(r => initializer?.Count() > r.Matches && r.Params?.Count() > r.Matches && r.Wildcard ? r.Matches + 1 : r.Matches)
                .ThenBy(r => r.Template?.Length);

            var bestRoute = matchedRoutes.First();
            var missing = initializer?.Where(kvp => bestRoute.Params?.Contains(kvp.Key) == true).FirstOrDefault().Value;
            var finalUri = bestRoute.Template ?? "";

            // 3. Replace the placeholders
            foreach (var kvp in initializer ?? [])
            {
                var pattern = $@"\{{\*?{kvp.Key}(?::.*?)?\}}";
                finalUri = System.Text.RegularExpressions.Regex.Replace(finalUri, pattern, kvp.Value ?? "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // replace catch all with missing value
            // TODO: replace intermittent? /*/something?
            if (missing != null && finalUri.EndsWith("/*"))
            {
                finalUri = finalUri.Substring(0, finalUri.Length - 1) + missing;
            }

            return finalUri;
        }

        public static (Type ComponentType, Dictionary<string, object?> Parameters) IdentifyNavigation(this Uri uri)
        {
            return IdentifyNavigation(uri.AbsolutePath);
        }

        public static (Type ComponentType, Dictionary<string, object?> Parameters) IdentifyNavigation(string uri)
        {
            var urlParts = uri.Split('?');
            var path = urlParts[0].Trim('/');
            var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var candidates = new List<(Type Type, ParsedRoute Route, Dictionary<string, object?> Params, int LiteralMatches)>();

            foreach (var entry in _routeCache)
            {
                foreach (var route in entry.Value)
                {
                    var templateSegments = route.Template?.Trim('/').Split('/') ?? Array.Empty<string>();

                    // 1. Structural Validation
                    if (!route.Wildcard && templateSegments.Length != pathSegments.Length) continue;
                    if (route.Wildcard && pathSegments.Length < templateSegments.Length - 1) continue;

                    var extractedParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    bool isMatch = true;
                    int literalMatches = 0;

                    for (int i = 0; i < templateSegments.Length; i++)
                    {
                        var tSeg = templateSegments[i];
                        bool isParam = tSeg.StartsWith('{') && tSeg.EndsWith('}');

                        if (isParam)
                        {
                            var paramName = tSeg.Trim('{', '}').Split(':')[0];
                            if (paramName.StartsWith('*'))
                            {
                                extractedParams[paramName.TrimStart('*')] = string.Join('/', pathSegments.Skip(i));
                                break;
                            }
                            if (i < pathSegments.Length)
                                extractedParams[paramName] = pathSegments[i];
                        }
                        else
                        {
                            if (i < pathSegments.Length && string.Equals(tSeg, pathSegments[i], StringComparison.OrdinalIgnoreCase))
                            {
                                literalMatches++;
                            }
                            else
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        candidates.Add((entry.Key, route, extractedParams, literalMatches));
                    }
                }
            }

            // 2. Prioritize: Most literal matches first, then shortest template (least greedy)
            var bestMatch = candidates
                .OrderByDescending(c => c.LiteralMatches)
                .ThenBy(c => c.Route.Template?.Length)
                .FirstOrDefault();

            if (bestMatch.Type == null)
                throw new InvalidOperationException($"No registered [Route] matches: {uri}");

            // 3. Append Query Parameters to the best match
            if (urlParts.Length > 1)
            {
                var queryPairs = urlParts[1].Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in queryPairs)
                {
                    var kvp = pair.Split('=', 2);
                    if (kvp.Length == 2)
                    {
                        bestMatch.Params[Uri.UnescapeDataString(kvp[0])] = Uri.UnescapeDataString(kvp[1]);
                    }
                }
            }

            return (bestMatch.Type, bestMatch.Params);
        }
    }
}
