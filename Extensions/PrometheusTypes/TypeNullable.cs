
namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {


        public static bool IsNullable(this ParameterInfo parameter)
        {
            var context = new NullabilityInfoContext();

            var paramInfo = context.Create(parameter);
            if (paramInfo.WriteState == NullabilityState.Nullable)
            {
                return true;
            }
            return false;
        }


        public static bool IsNullable(this Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }


        public static bool IsNullable(this MethodInfo method)
        {
            var context = new NullabilityInfoContext();

            // 1. Check the Return Type (e.g., public string? GetName())
            var returnInfo = context.Create(method.ReturnParameter);
            if (returnInfo.ReadState == NullabilityState.Nullable)
            {
                return true;
            }
            return false;
        }


        public static bool IsNullable(this MemberInfo member)
        {
            return member switch
            {
                PropertyInfo prop => prop.IsNullable(),
                FieldInfo field => field.FieldType.IsNullable(),
                MethodInfo method => method.ReturnType.IsNullable(),
                Type type => type.IsNullable(), // Handle the case where the member IS a type
                _ => throw new InvalidOperationException($"Member type {member.GetType().Name} is not supported.")
            };
        }

        public static bool IsNullable(this PropertyInfo property)
        {
            var info = context.Create(property);

            if (property.PropertyType.IsNullable()
                || info.WriteState == NullabilityState.Nullable
                || info.ReadState == NullabilityState.Nullable)
            {
                return true;
            }
            return false;
        }

    }
}
