namespace RazorSharp.Services
{
    /*
    public class EnumConstraint<T> : IRouteConstraint where T : struct, Enum
    {
        private static readonly HashSet<string> _validValues;

        static EnumConstraint()
        {
            // Cache all possible matches: Names and Descriptions
            _validValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (T value in Enum.GetValues<T>())
            {
                // Add the name (e.g., "Multi")
                _validValues.Add(value.ToString());

                // Add the description (e.g., "funnel")
                var field = typeof(T).GetField(value.ToString());
                var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
                if (attribute != null)
                {
                    _validValues.Add(attribute.Description);
                }
            }
        }

        public bool Match(IServerState? IServerState, IRouter? route, string routeKey,
            RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (values.TryGetValue(routeKey, out var value) && value != null)
            {
                var stringValue = value.ToString();
                return !string.IsNullOrWhiteSpace(stringValue) && _validValues.Contains(stringValue);
            }
            return false;
        }
    }
    */
}