
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Interfacing.Services;

public interface IHasModule
{
    ValueTask EnsureInitialized();
    bool IsReady { get; }
}


// runs as singleton on desktop and scoped in web mode, IRenderState requires this, ironically, to stay decoupled
//   mark these words, nothing other than IRenderState uses this mode
public interface ISingleUser
{

}


public interface ISingleton
{

}


public interface IHasChanged
{
    Task HasChanged(ICompositeProvider? Composite);
}


public interface IRenderState : IHasModule, ISingleUser
{
    object Runtime { get; }
    event Action OnRendered;
    event Action OnEmptied;
    void NotifyEmptied(object? runtime);
    void NotifyRendered(object? runtime);



    // page data handling instead of built in MS uninspectable crap
    Task<Dictionary<string, string?>?> RestoreState(object component);
    bool SetState(object? state);
    event Action<object?>? OnStateChanged;
    Dictionary<string, string?> State { get; set; }

    void ClearRedirect();
    ConcurrentDictionary<string, string?> InFlight { get; }
    Task<string?> FilterRedirect(string url);

}


public interface IHasErrors
{
    event Action<Exception?>? OnErrorChanged;
    Task SetError(Exception? error);
}

public interface IHasErrors<T> : IHasErrors
{
    static abstract ConcurrentQueue<(DateTime Created, Exception Exception)> Immediate { get; }
}


public interface IHasClass
{
    ClassNameCollection ClassNames { get; }
    void SetClasses(List<string>? classes);
    void SetTheme(string? classes);
    void SetSidebar(string? classes);
    void SetUri(string uri);
    string? Sidebar { get; }
    Action<object> LogoContent { get; }

    void SetRoute(Type? typeHint);

    Type? RouteHint { get; }
    List<string> Registry { get; }

}


public interface IHasHeader
{
    Task RenderHeader(ICompositeProvider? Composite, IHasClass? Classy);
}


public interface IHasAnimation
{
    ValueTask InitializeBackground(string mode, string canvas);
    Task InitializeRender();
    void SetBackground(string? classes);
}


public interface IPageState
{
    Task<Dictionary<string, string?>?> RestoreState(object? runtime);
    Task<int> GetTimezoneOffset();
    int OffsetInMinutes { get; }
    void NotFound(); // set not found status in http context is available

    bool PageNotFound { get; }
}

public interface ISettings
{
    Task SaveSetting(string key, string value);

    Task<string> GetSetting(string key, string value);
}


public static partial class StateExtensions
{
    [GeneratedRegex(@"[^a-zA-Z0-9]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SafeRegex();

    public static string? SetState(this object? state)
    {
        // TODO: move this error message to new render state implementer
        //if (OperatingSystem.IsBrowser())
        //{
        //    throw new InvalidOperationException("This probably wont work from the web client.");
        //}
        if (state == null)
        {
            return null;
        }
        return state.ToSerialized();
    }

    public static Dictionary<string, string?>? RestoreState(this object component, string? componentState)
    {
        // TODO: move this error message to new render state implementer
        //if (!OperatingSystem.IsBrowser())
        //{
        //    throw new InvalidOperationException("This probably wont work from server.");
        //}
        Console.WriteLine("Restoring: " + component.GetType().Name);
        if (componentState == null)
        {
            return null;
        }

        var deserializedState = JsonSerializer.Deserialize<Dictionary<string, string?>>(componentState);
        Console.WriteLine("Deserializing: " + componentState);
        if (deserializedState == null)
        {
            return null;
        }
        component.ToProperties(deserializedState);
        return deserializedState;
    }

    internal static string ToSafe(this string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        string[] words = SafeRegex().Split(url);
        TextInfo ti = CultureInfo.CurrentCulture.TextInfo;

        var titleCasedWords = words
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => ti.ToTitleCase(w.ToLower()));

        string result = string.Join("", titleCasedWords);
        return result[..Math.Min(result.Length, 100)];
    }

    internal static string ToSerialized<TComponent>(this TComponent component) where TComponent : class
    {
        Dictionary<string, string?> result = [];
        var props = component.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(p => (
                prop: p,
                include: p.GetCustomAttributes<JsonPropertyNameAttribute>().FirstOrDefault()
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

    internal static void ToProperties<TComponent>(this TComponent component, Dictionary<string, string?> pageValues) where TComponent : class
    {
        var props = component.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(p => (
                prop: p,
                include: p.GetCustomAttributes<JsonPropertyNameAttribute>().FirstOrDefault()
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
                genericMethod = typeof(StateExtensions).GetMethod(nameof(StateExtensions.TryParse), 1, [typeof(string)])!.MakeGenericMethod(generalType);
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

    internal static TEnum? TryParse<TEnum>(this string val) where TEnum : struct, Enum
    {
        return TryParse<TEnum>((object)val);
    }

    // fuck i forget about this: An object reference is required for the non-static field, method, or property 'PrimitiveExtensions.TryParse<PackMode>(PackMode, object)'
    //public static TEnum? TryParse<TEnum>(this TEnum type, object val) where TEnum : struct, Enum
    //{
    //    return TryParse<TEnum>(val);
    //}

    internal static TEnum? TryParse<TEnum>(object val) where TEnum : struct, Enum
    {
        if (val is int love || int.TryParse(val.ToString(), out love))
        {
            return (TEnum?)Enum.ToObject(typeof(TEnum), love);
        }

        // ignore case because comes from a url
        if (val is string && Enum.TryParse<TEnum>(val.ToString(), true, out var love3))
        {
            return (TEnum?)love3;
        }

        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (string.Equals(value.ToString(), val.ToString(), StringComparison.InvariantCultureIgnoreCase))
            {
                return (TEnum?)Enum.ToObject(typeof(TEnum), value);
            }
            var attribute = typeof(TEnum).GetField(value.ToString())?.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault();
            if (attribute != null && string.Equals(val.ToString(), Enum.GetName(value)) || string.Equals(val.ToString(), attribute?.Description, StringComparison.InvariantCultureIgnoreCase))
            {
                return (TEnum?)Enum.ToObject(typeof(TEnum), value);
            }
        }

        return null;
    }

}