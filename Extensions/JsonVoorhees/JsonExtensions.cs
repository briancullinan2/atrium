
using Extensions.Utilities.Extensions;

namespace Extensions.JsonVoorhees
{
    public static partial class JsonExtensions
    {
        public static string ToSerialized<TComponent>(this TComponent component) where TComponent : class
        {
            Dictionary<string, string?> result = [];
            var props = component.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(p => (
                    prop: p,
                    include: p.GetCustomAttribute<JsonPropertyNameAttribute>()
                    ))
                .Where(p => p.include != null);
            foreach (var (prop, include) in props)
            {
                object? value = prop.GetValue(component);
                if (value == null) continue;
                var storageName = component.GetType().Name + "." + include!.Name;
                var generalType = (Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                if (generalType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(generalType))
                {
                    var bs = ((System.Collections.IEnumerable)value).Cast<object?>().ToList();
                    result[storageName] = JsonSerializer.Serialize(bs);
                }
                else if (generalType.IsEnum)
                {
                    result[storageName] = JsonSerializer.Serialize(value.ToString());
                }
                else
                {
                    result[storageName] = JsonSerializer.Serialize(value);
                }
                Console.WriteLine("Persisted: " + value);
            }
            return JsonSerializer.Serialize(result);
        }

        public static void ToProperties<TComponent>(this TComponent component, Dictionary<string, string?> pageValues) where TComponent : class
        {
            var props = component.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(p => (
                    prop: p,
                    include: p.GetCustomAttribute<JsonPropertyNameAttribute>()
                ))
                .Where(p => p.include != null);
            var method = typeof(JsonSerializer)
                .GetMethod(nameof(JsonSerializer.Deserialize), 1, [typeof(string), typeof(JsonSerializerOptions)]);
            foreach (var (prop, include) in props)
            {
                var storageName = component.GetType().Name + "." + include!.Name;
                MethodInfo genericMethod;
                var generalType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (generalType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(generalType))
                {
                    var genericList = typeof(List<>).MakeGenericType(generalType.GenericTypeArguments[0]);
                    genericMethod = method!.MakeGenericMethod(genericList);
                    // TODO: SetValue(ToCollection) bullshit?
                }
                else if (generalType.IsEnum)
                {
                    genericMethod = typeof(StringExtensions).GetMethod(nameof(StringExtensions.TryParse), 1, [typeof(string)])!.MakeGenericMethod(generalType);
                }
                else
                {
                    genericMethod = method!.MakeGenericMethod(generalType);
                }
                // TODO: this needs to be from the page
                _ = pageValues.TryGetValue(storageName, out string? propSerialized);
                if (propSerialized == null)
                {
                    continue;
                }

                object? success = null;
                if (generalType.IsEnum)
                {
                    success = genericMethod.Invoke(null, [JsonSerializer.Deserialize<string>(propSerialized)]);
                }
                else
                {
                    success = genericMethod.Invoke(null, [propSerialized, null]);
                }
                if (success != null)
                {
                    var val = success;
                    Console.WriteLine("Recovered: " + val);
                    if (generalType.IsEnum && val != null && val is not Enum)
                    {
                        val = Enum.ToObject(prop.PropertyType, val);
                    }
                    prop.SetValue(component, val);
                }
            }
        }


    }
}
