

using System.Collections;
using System.ComponentModel;

namespace Atrium.Services;

public class PageManager(ICompositeProvider Composite, IRenderState Rendered) : IHasEvents
{
    private readonly Dictionary<(PageAction Action, string? Id), object?> _states = [];

    private readonly Dictionary<(PageAction Action, string? Id), Delegate?> _events = [];
    public bool IsReady => _restartRequired.Task.IsCompleted && _restartRequired.Task.Result == true;

    public void Unsubscribe(PageAction Action, string? key, Delegate? value)
    {
        if (value == null)
        {
            //_events.Remove(key);
            return;
        }

        if (_events.TryGetValue((Action, key), out var existing))
        {
            _events[(Action, key)] = Delegate.Remove(existing, value);
        }
        else
            _events[(Action, key)] = null;
        // Combine adds 'value' to the invocation list of 'existing'

        // THE AUTO-FIRE ENGINE
        TriggerState((Action, key), value);
    }

    public void Subscribe(PageAction Action, Delegate? value)
    {
        Subscribe(Action, "window", value);
    }

    public void Subscribe(PageAction Action, string? key, Delegate? value)
    {
        if (value == null)
        {
            //_events.Remove(key);
            return;
        }

        if (_events.TryGetValue((Action, key), out var existing))
        {
            _events[(Action, key)] = Delegate.Remove(existing, value);
            _events[(Action, key)] = Delegate.Combine(existing, value);
        }
        else
            _events[(Action, key)] = value;
        // Combine adds 'value' to the invocation list of 'existing'

        // THE AUTO-FIRE ENGINE
        TriggerState((Action, key), value);
    }




    private void TriggerState((PageAction Action, string? Id) key, Delegate? value, object? newState = null)
    {
        if (value == null) return;
        var lastState = newState ?? (_states.TryGetValue(key, out var state) ? state : null);
        var possibleValues = (lastState as IEnumerable)?.Cast<object?>();
        var possibleEl = ((newState is JsonElement el2) ? el2.ToObject() : null);

        if (newState == null && lastState == null) return;

        try
        {
            var consumed = 0;
            var parameters = value.Method.GetParameters();
            object?[] inputParameters = new object?[parameters.Length];
            for (var i = 0; i < parameters?.Length; i++)
            {
                var val = possibleValues?.ElementAtOrDefault(consumed) ?? possibleEl ?? lastState;
                if (possibleEl is IEnumerable enumerable)
                    val = enumerable.Cast<object?>().ElementAtOrDefault(consumed);

                if (val?.GetType().Extends(parameters[i].ParameterType) == true)
                {
                    inputParameters[i] = val;
                    consumed++;
                }
            }

            value?.InvokeService(Composite, inputParameters);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
            
    }




    public async ValueTask TriggerEvent(string eventName, object? detail = null)
    {
        await EnsureInitialized();
        OnPageEvent(eventName, detail);
        if(Module?.InvokeVoidAsync("dispatchEvent", eventName, detail) is ValueTask task) await task;
    }

    private TaskCompletionSource<bool> _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    IJSObjectReference? Module = null;
    private DotNetObjectReference<PageManager>? dotNetHelper;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public async ValueTask EnsureInitialized()
    {
        // 1. Quick check outside the lock for performance
        if (_restartRequired.Task.IsCompleted) return;

        // 2. Wait for the lock
        if (!await _loadLock.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false))
        {
            // If we hit this, we know we have a deadlock or a hung JS call
            throw new TimeoutException("Initialization lock timed out. Possible deadlock in JS/Rendered.");
        }

        try
        {
            if (_restartRequired.Task.IsCompleted) return;
            await Rendered.EnsureInitialized();
            if (((IJSRuntime)Rendered.Runtime).InvokeAsync<IJSObjectReference>("import", "/connect.js") is ValueTask<IJSObjectReference> task)
                Module = await task;
            dotNetHelper = DotNetObjectReference.Create(this);
            var methods = GetType().GetMethods()
                .Select(m => m.Name)
                .ToArray();
            if (Module?.InvokeVoidAsync("register", GetType().FullName, dotNetHelper, methods, true) is ValueTask task2)
                await task2;

            _restartRequired.TrySetResult(true);
        }
        catch (Exception ex)
        {
            if(Module != null)
                await Module.DisposeAsync();
            dotNetHelper = null;
            Console.WriteLine(ex);
            throw;
        }
        finally
        {
            // 4. Always release the lock in a finally block
            _loadLock.Release();
        }
    }


    public async ValueTask TriggerEvent(PageAction eventName, object? detail = null)
    {
        await EnsureInitialized();
        OnPageEvent(eventName, detail);
        if(Module?.InvokeVoidAsync("dispatchEvent", eventName.ToString(), detail) is ValueTask task)
            await task;
    }

    [JSInvokable] public void OnPageEvent(string id, object? detail = null) => UpdateStateDebouncer(id.TryParse<PageAction>() ?? PageAction.Action, "window", detail);
    public void OnPageEvent(PageAction id, object? detail = null) => UpdateStateDebouncer(id, "window", detail);


    protected void UpdateStateDebouncer(PageAction action, string id, object? value)
    {
        PageExtensions.Debounce(
            UpdateState, 100, action, id, value
            );
    }


    // 2. Unified UpdateState that handles the "Replay" logic
    protected void UpdateState(PageAction action, string? id, object? value)
    {
        if (id == null) return;
        var key = (action, id);
        _states[key] = value;
        if (_events.TryGetValue(key, out var del))
        {
            TriggerState(key, del, value);
        }
        if (id != "window" && _events.TryGetValue((action, "window"), out var del2))
        {
            TriggerState(key, del2, value);
        }
    }


    [JSInvokable("GetService")]
    public async Task<string?> GetService(string typeName)
    {
        var type = Type.GetType(typeName) ?? throw new InvalidOperationException("type not found: " + typeName);
        var service = Composite?.GetService(type) ?? throw new InvalidOperationException("service not found: " + type.FullName);
        var objRef = DotNetObjectReference.Create(service);
        var methods = this.GetType().GetMethods()
            .Select(m => m.Name)
            .ToArray();
        if (Module != null)
            await Module.InvokeVoidAsync("register", service.GetType().FullName, objRef, methods, false);
        return service.GetType().FullName ?? service.GetType().Name;
    }


    [JSInvokable("Invoke")]
    public object? Invoke(string methodName, JsonElement[] args)
    {
        var type = GetType();
        var method = type.GetMethods()
            .Where(m => m.Name.Equals(methodName, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(m => m.ContainsGenericParameters)
            .FirstOrDefault()
            ?? throw new MissingMethodException($"Sentry: Method '{methodName}' not found on {type.Name}.");

        var parameters = method.GetParameters();
        var convertedArgs = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            if (i < args.Length)
            {
                convertedArgs[i] = args[i].Deserialize(parameters[i].ParameterType);
            }
        }

        // 4. Execute and Return
        if (method.IsStatic)
        {
            return method.Invoke(null, convertedArgs);
        }
        else
        {
            return method.Invoke(this, convertedArgs);
        }
    }
}


internal static class PageExtensions
{

    public static Task Debounce<T1, T2, T3>(
        this Action<T1?, T2?, T3?> action,
        int delay = 200,
        T1? t1 = default,
        T2? t2 = default,
        T3? t3 = default,
        CancellationToken? token = null,
        [CallerFilePath] string file = "",
        [CallerMemberName] string key = "")
        => Debounce(action: async ct => { action(t1, t2, t3); return true; }, delay, token, file, key);

    private static readonly ConcurrentDictionary<string, object> registry = new();

    public static async Task<T?> Debounce<T>(
        this Func<CancellationToken, Task<T>> action,
        int delay = 200,
        CancellationToken? token = null,
        [CallerFilePath] string file = "", [CallerMemberName] string key = "")
    {
        // Fix for the "System.Private.CoreLib" issue: 
        // Get the Entry Assembly name to scope the key to your specific app.
        var assembly = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
        var uniqueKey = $"{assembly}_{file}_{key}";

        var tcs = new TaskCompletionSource<T?>();
        var cts = new CancellationTokenSource();
        if (token != null)
            token?.Register(cts.Cancel);
        var entry = (tcs, cts);

        // Update the registry: Cancel the old one, but DO NOT Dispose it here.
        registry.AddOrUpdate(uniqueKey, entry, (_, old) =>
        {
            var (oldTcs, oldCts) = ((TaskCompletionSource<T?>, CancellationTokenSource))old;
            oldCts.Cancel();
            // oldCts.Dispose(); // <--- REMOVE THIS. It causes the ObjectDisposedException.
            return entry;
        });

        try
        {
            // Use the token from the NEWEST CTS
            await Task.Delay(delay, cts.Token);

            var result = await action(cts.Token);

            tcs.TrySetResult(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            // Request Collapsing: Wait for the result of whoever superseded us.
            if (registry.TryGetValue(uniqueKey, out var latest) &&
                latest is (TaskCompletionSource<T?> latestTcs, _))
            {
                return await latestTcs.Task;
            }
            return default;
        }
        finally
        {
            // Only the "last" one standing cleans up the registry
            if (registry.TryGetValue(uniqueKey, out var current) && current.Equals(entry))
            {
                registry.TryRemove(uniqueKey, out _);
            }

            // Safely dispose of our own resources now that we are done.
            cts.Dispose();
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



    public static object? ToObject(this JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDecimal(), // Or GetDouble() depending on precision needs
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(x => x.Name, x => ToObject(x.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToList(),
            JsonValueKind.Undefined => null,
            _ => throw new NotSupportedException($"Unsupported JSON type: {element.ValueKind}")
        };
    }

}

