
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Interfacing.Services;

public interface IHasModule
{
    ValueTask EnsureInitialized();
    bool IsReady { get; }
}


public interface IRenderState : IHasModule
{
    object Runtime { get; }
    event Action OnRendered;
    event Action OnEmptied;
    void NotifyEmptied();
    void NotifyRendered(object Runtime);



    // page data handling instead of built in MS uninspectable crap
    Task<Dictionary<string, string?>?> RestoreState(object component);
    Task SetState(object? state);
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

    void SetPageClasses(List<string> classes);
    void SetTheme(string? classes);
    void SetSidebar(string? classes);
    string? Sidebar { get; }
}


public interface IHasAnimation
{
    ValueTask InitializeBackground(string mode, string canvas);
    Task InitializeRender();
    void SetBackground(string? classes);
}


public interface IPageState
{
    string ConnectionId { get; }
    Task<Dictionary<string, string?>?> RestoreState();
    Task<int> GetTimezoneOffset();
    int OffsetInMinutes { get; }
}




// TODO: add component state saving stuff here because its also agnostic and small

public class RenderStateProvider(IPageState Page) : IRenderState
{

    private object? _runtime = null;
    public object Runtime
    {
        get
        {
            if (!_renderTcs.Task.IsCompleted || _runtime == null)
            {
                throw new InvalidOperationException("JSRuntime is not available. Ensure that the component is rendered before registering for scroll events.");
            }
            return _runtime;
        }
        private set => _runtime = value;
    }

    // This is the task your LocalStore will 'Then' off of
    private Action? _onRendered;
    public event Action? OnRendered
    {
        add
        {
            _onRendered += value;
            // The "Sticky" logic: If the condition is already met, 
            // fire the callback for this specific subscriber immediately.
            if (IsReady)
            {
                value?.Invoke();
            }
        }
        remove => _onRendered -= value;
    }
    public event Action? OnEmptied;



    private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask EnsureInitialized() => await _renderTcs.Task;

    public bool IsReady => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

    public void NotifyRendered(object runtime)
    {
        Runtime = runtime;
        // Fulfill the promise for everyone currently waiting
        _renderTcs.TrySetResult(true);
        //OffsetInMinutes = await Page.GetTimezoneOffset();
        _onRendered?.Invoke();
    }

    public void NotifyEmptied()
    {
        _runtime = null;
        // Only swap for a new "Promise" if the old one was already fulfilled.
        // If it's still pending, let the current waiters keep waiting for the NEXT render.
        if (_renderTcs.Task.IsCompleted)
        {
            _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        OnEmptied?.Invoke();
    }



    // TODO: move this to IRenderState to free up IPageEvents to only deal with eventing

    public int OffsetInMinutes { get; private set; }
    public ConcurrentDictionary<string, string?> InFlight { get; } = [];

    public Dictionary<string, string?> State { get; set; } = [];

    public event Action<object?>? OnStateChanged;

    public void ClearRedirect()
    {
        if (InFlight.ContainsKey(Page.ConnectionId))
            InFlight.Remove(Page.ConnectionId, out _);
    }


    // prevent redirect loops
    public async Task<string?> FilterRedirect(string loginUri)
    {

        // 2. Logic to prevent redirect loops or "double stacking"
        InFlight.TryGetValue(Page.ConnectionId, out var existing);

        //var loginUri = TypeExtensions.GetUri<ILogin>(l => l.ReturnUrl == url);

        // Update the InFlight status
        InFlight[Page.ConnectionId] = loginUri;

        // 3. Perform the navigation only if we aren't already heading to login
        if (existing?.Contains("login", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }

        return loginUri;
        // 'forceLoad: true' triggers a full browser refresh/intercept, 
        // which is standard for Auth redirects.
        //Nav.NavigateTo(loginUri, forceLoad: true);
    }


    public virtual async Task SetState(object? state)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new InvalidOperationException("This probably wont work from the web client.");
        }
        if (state == null)
        {
            return;
        }
        State[state.GetType().Name.ToSafe()] = state.ToSerialized();
        OnStateChanged?.Invoke(state);
    }

    public virtual async Task<Dictionary<string, string?>?> RestoreState(object component)
    {
        if (!OperatingSystem.IsBrowser())
        {
            throw new InvalidOperationException("This probably wont work from server.");
        }
        await EnsureInitialized();
        var state = await Page.RestoreState();
        if (state?.TryGetValue("state_" + component.GetType().Name.ToSafe(), out string? componentState) == true)
        {
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
        }
        return state;
    }

}


internal static partial class StateExtensions
{
    [GeneratedRegex(@"[^a-zA-Z0-9]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SafeRegex();

    public static string ToSafe(this string url)
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

    public static string ToSerialized<TComponent>(this TComponent component) where TComponent : class
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

    public static void ToProperties<TComponent>(this TComponent component, Dictionary<string, string?> pageValues) where TComponent : class
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

    public static TEnum? TryParse<TEnum>(this string val) where TEnum : struct, Enum
    {
        return TryParse<TEnum>((object)val);
    }

    // fuck i forget about this: An object reference is required for the non-static field, method, or property 'PrimitiveExtensions.TryParse<PackMode>(PackMode, object)'
    //public static TEnum? TryParse<TEnum>(this TEnum type, object val) where TEnum : struct, Enum
    //{
    //    return TryParse<TEnum>(val);
    //}

    public static TEnum? TryParse<TEnum>(object val) where TEnum : struct, Enum
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