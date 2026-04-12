using Microsoft.Extensions.Logging;
using System.Collections;

namespace RazorSharp.Services;



public class PageManager : IPageManager
{

    #region "Initialization"
    public ConcurrentDictionary<string, string?> InFlight { get; } = [];

    private List<string> GivenClassNames { get; set; } = [];
    private ClassNameCollection CombinedClassNames { get; } = [];
    public ClassNameCollection ClassNames { get => CombinedClassNames; set => GivenClassNames = [..value]; }


    //public string ContextKey => Form?.ConnectionId ?? string.Empty;

    public bool IsReady => _restartRequired.Task.IsCompleted && _restartRequired.Task.Result == true;

    private TaskCompletionSource<bool> _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static ConcurrentQueue<(DateTime Created, Exception Exception)> Immediate { get; set; } = [];

    public Dictionary<string, string?> State { get; set; } = [];

    public event Action<object?>? OnStateChanged;
    public event Action<Exception?>? OnErrorChanged;

    //readonly IFormFactor? Form;
    readonly ILoggerFactory Logger;
    readonly IRenderState Rendered;
    //readonly ICircuitProvider? Context;
    private readonly IAuthService? Auth;
    readonly NavigationManager Nav;


    public IJSRuntime? Runtime => Rendered.Runtime as IJSRuntime;

    public PageManager(
        ILoggerFactory _logger,
        IRenderState _rendered,
        NavigationManager _nav,
        //ICircuitProvider? _context = null,
        IAuthService? _auth = null
    ) : base() {
        Nav = _nav;
        Logger = _logger;
        Rendered = _rendered;
        //Context = _context;
        Auth = _auth;
        Rendered.OnEmptied += NotifyEmptied;
        Rendered.OnRendered += NotifyRendered;
        Nav.LocationChanged += Nav_LocationChanged;

        CombinedClassNames.AutoSources = () => [
            Theme,
            Sidebar,
            Background,
            .. (PageClasses ?? []),
            .. GivenClassNames
        ];
    }

    private void Nav_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Nav.Uri.Trim('/'))) PageClasses = ["Home"];
        PageClasses = [..Nav.ToBaseRelativePath(Nav.Uri.Split('?')[0])
            .Split('/')
            .Select(seg => seg.ToSafe())
        ];
    }

    protected void NotifyEmptied() 
    {
        CombinedClassNames?.Add("cover-page");
        CombinedClassNames?.Add("login-mode");
        if (_restartRequired.Task.IsCompleted)
            _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }


    protected void NotifyRendered() => _ = EnsureModuleLoaded();


    public async ValueTask DisposeAsync()
    {
        Rendered.OnRendered -= NotifyEmptied;
        Rendered.OnEmptied -= NotifyEmptied;
        if (IsReady)
        {
            await Module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }


    public Task ModuleInitialize => _restartRequired.Task;


    private IJSObjectReference? _module = null;
    private List<string>? PageClasses = [];
    public string? Theme;
    public string? Sidebar { get; private set; }
    public string? Background;

    public IJSObjectReference Module
    {
        get
        {
            if (!_restartRequired.Task.IsCompleted || _module == null)
            {
                throw new InvalidOperationException("Module is not available. Must await ModuleInitialize before refering to JS module.");
            }
            return _module;
        }
        private set => _module = value;
    }


    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public async Task EnsureModuleLoaded()
    {
        // 1. Quick check outside the lock for performance
        if (_restartRequired.Task.IsCompleted) return;

        // 2. Wait for the lock
        await _loadLock.WaitAsync();

        try
        {
            if (_restartRequired.Task.IsCompleted) return;
            Module = await (Rendered.Runtime as IJSRuntime)!.InvokeAsync<IJSObjectReference>("import", "/_content/RazorSharp/page.js");
            var dotNetHelper = DotNetObjectReference.Create(this);
            _restartRequired.TrySetResult(true);
            await Module.InvokeVoidAsync("subscribePageEvents", dotNetHelper);
            OffsetInMinutes = await (Rendered.Runtime as IJSRuntime)!.InvokeAsync<int>("eval", "new Date().getTimezoneOffset()");
            ClassNames.Remove("cover-page");
            // let login manager remove login mode?
            if (Auth == null)
                ClassNames.Remove("login-mode");
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            // 4. Always release the lock in a finally block
            _loadLock.Release();
        }
    }

    #endregion


    #region "Page State"

    // TODO: move this to IRenderState to free up IPageEvents to only deal with eventing

    public int OffsetInMinutes { get; private set; }

    public void ClearRedirect()
    {
        //if (InFlight.ContainsKey(ContextKey))
        //    InFlight.Remove(ContextKey, out _);
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
        State[state.GetType().Name.ToSafe()] = JsonExtensions.ToSerialized(state);
        OnStateChanged?.Invoke(state);
    }

    public virtual async Task<Dictionary<string, string?>?> RestoreState(object component)
    {
        if(!OperatingSystem.IsBrowser())
        {
            throw new InvalidOperationException("This probably wont work from server.");
        }
        await ModuleInitialize;
        var state = await Module.InvokeAsync<Dictionary<string, string?>>("restoreState");
        _ = state.TryGetValue("state_" + component.GetType().Name.ToSafe(), out string? componentState);
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
        JsonExtensions.ToProperties(component, deserializedState);
        return state;
    }

    public async Task SetError(Exception? error)
    {
        if (error == null)
        {
            Immediate.Clear();
            return;
        }
        Immediate.Enqueue((DateTime.Now, error));
        if (Immediate.Count > 10
            // start deleting old records
            || !Immediate.IsEmpty && Immediate.First().Created.AddMinutes(3) < DateTime.Now)
        {
            Immediate.TryDequeue(out _);
        }
        OnErrorChanged?.Invoke(error);
    }


    public async Task<MarkupString> Copy(RenderFragment? _activeBody, IServiceProvider Services)
    {
        var fragment = _activeBody;
        using var htmlRenderer = new HtmlRenderer(Services, Logger);

        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment wrappedFragment = builder =>
            {
                builder.OpenComponent<CascadingValue<bool>>(0);
                builder.AddAttribute(1, "Name", "IsStaticRender");
                builder.AddAttribute(2, "Value", true);
                builder.AddAttribute(3, "ChildContent", fragment);
                builder.CloseComponent();
            };

            var output = await htmlRenderer.RenderComponentAsync<ContentWrapper>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                { "ChildContent", wrappedFragment }
                }));
            return output.ToHtmlString();
        });
        return new MarkupString(html);
    }


    public async Task<bool> IsAtBottomAsync(string id)
    {
        await ModuleInitialize;
        return await Module.InvokeAsync<bool>("isAtBottom", id);
    }
    

    public async Task ScrollSlightlyAsync(string id, int amount = 10)
    {
        await ModuleInitialize;
        await Module.InvokeVoidAsync("scrollSlightly", id, amount);
    }
        

    public async Task<string> GetLineHeightAsync(string? elementId = null)
    {
        await ModuleInitialize;
        return await Module.InvokeAsync<string>("getLineHeight", elementId);
    }


    public async Task<int> GetLineHeightIntAsync(string? elementId = null)
    {
        await ModuleInitialize;
        return await Module.InvokeAsync<int>("getLineHeightInt", elementId);
    }


    #endregion


    #region "Page Events"



    public async Task RegisterAsync(string id)
    {
        await ModuleInitialize;
        await Module.InvokeVoidAsync("subscribeScroll", id);
    }



    public bool IsLocked(string id) => _states.TryGetValue((PageAction.Scroll, id), out var atBottom) 
        && atBottom is bool asBool && asBool == true;



    public async Task StartBlazor()
    {
        await ModuleInitialize;
        await Module.InvokeVoidAsync("startBlazor");
    }


    public async Task ScrollToBottom(string id, bool smooth = true)
    {
        await ModuleInitialize;
        await Module.InvokeVoidAsync("scrollToBottom", id, smooth);
    }

    public async Task<Dictionary<string, bool>> GetAllStatesAsync(string[]? ids = null)
    {
        await ModuleInitialize;
        return await Module.InvokeAsync<Dictionary<string, bool>>("getScrollStates", ids);
    }

    private readonly Dictionary<(PageAction Action, string Id), object?> _states = [];

    // Tracks the actual multicast delegates per action/id pair
    private readonly Dictionary<(PageAction Action, string Id), Delegate?> _events = [];


    public event Func<int, int, bool, Task> OnResizeAsync
    {
        add
        {
            Subscribe((PageAction.Resize, "window"), value);
        }
        remove
        {
            Unsubscribe((PageAction.Resize, "window"), value);
        }
    }

    public event Action<int, int, bool> OnResize
    {
        add
        {
            Subscribe((PageAction.Resize, "window"), value);
        }
        remove
        {
            Unsubscribe((PageAction.Resize, "window"), value);
        }
    }
    public event Action<string, bool> OnFocus
    {
        add
        {
            Subscribe((PageAction.Focus, "window"), value);
        }
        remove
        {
            Unsubscribe((PageAction.Visible, "window"), value);
        }
    }
    public event Action<string, bool> OnScroll
    {
        add
        {
            Subscribe((PageAction.Scroll, "window"), value);
        }
        remove
        {
            Unsubscribe((PageAction.Visible, "window"), value);
        }
    }
    public event Action<bool> OnVisible
    {
        add
        {
            Subscribe((PageAction.Visible, "window"), value);
        }
        remove
        {
            Unsubscribe((PageAction.Visible, "window"), value);
        }
    }
    public event Action<string> OnReconnect
    {
        add
        {
            Subscribe((PageAction.Reconnect, "window"), value);
        }
        remove
        {
            Unsubscribe((PageAction.Reconnect, "window"), value);
        }
    }
    //public BooleanProxy OnScroll => new(this, PageAction.Scroll);
    //public BooleanAsyncProxy OnScrollAsync => new(this, PageAction.Scroll);
    //private ResizeProxy OnResizePattern => new(this);
    //public BooleanProxy OnFocus => new(this, PageAction.Focus);
    //public BooleanAsyncProxy OnFocusAsync => new(this, PageAction.Focus);
    //public StringProxy OnVisible => new(this, PageAction.Visible);
    //public StringProxy OnReconnect => new(this, PageAction.Reconnect);



    //public Delegate? this[PageAction action, string id]
    //{
    //    get => _events.TryGetValue((action, id), out var del) ? del : null;
    //    set => Subscribe((action, id), value);
    //}
    

    public void Unsubscribe((PageAction Action, string Id) key, Delegate? value)
    {
        if (value == null)
        {
            //_events.Remove(key);
            return;
        }

        if (_events.TryGetValue(key, out var existing))
        {
            _events[key] = Delegate.Remove(existing, value);
        }
        else
            _events[key] = null;
        // Combine adds 'value' to the invocation list of 'existing'

        // THE AUTO-FIRE ENGINE
        TriggerState(key, value);
    }

    public void Subscribe((PageAction Action, string Id) key, Delegate? value)
    {
        if (value == null)
        {
            //_events.Remove(key);
            return;
        }

        if (_events.TryGetValue(key, out var existing))
        {
            _events[key] = Delegate.Remove(existing, value);
            _events[key] = Delegate.Combine(existing, value);
        }
        else
            _events[key] = value;
        // Combine adds 'value' to the invocation list of 'existing'

            // THE AUTO-FIRE ENGINE
        TriggerState(key, value);
    }


    // prevent redirect loops
    public async Task Redirect(string url)
    {

        // 2. Logic to prevent redirect loops or "double stacking"
        //InFlight.TryGetValue(ContextKey, out var existing);

        //var loginUri = TypeExtensions.GetUri<ILogin>(l => l.ReturnUrl == url);

        // Update the InFlight status
        //InFlight[ContextKey] = loginUri;

        // 3. Perform the navigation only if we aren't already heading to login
        //if (existing?.Contains("login", StringComparison.OrdinalIgnoreCase) == true)
        //{
        //    return;
        //}

        // 'forceLoad: true' triggers a full browser refresh/intercept, 
        // which is standard for Auth redirects.
        //Nav.NavigateTo(loginUri, forceLoad: true);
    }

    private void TriggerState((PageAction Action, string Id) key, Delegate? value, object? newState = null)
    {
        var lastState = newState ?? (_states.TryGetValue(key, out var state) ? state : null);

        if (newState != null || lastState != null)
        {
            // Pattern match the action to know how to 'Replay' the state
            switch (key.Action)
            {
                case PageAction.Visible:
                    if (value is Action<bool> visibleHandler
                        && lastState is bool visible)
                        visibleHandler.Invoke(visible);
                    else if (value is Action<bool?> visibleHandler2)
                        visibleHandler2.Invoke(lastState as bool?);
                    break;
                case PageAction.Reconnect:
                    if (value is Action<string> stateHandler
                        && lastState is string reconnect)
                        stateHandler.Invoke(reconnect);
                    else if (value is Action<string?> stateHandler2)
                        stateHandler2.Invoke(lastState as string);
                    break;
                case PageAction.Scroll:
                case PageAction.Focus:
                    if (value is Action<string, bool> boolHandler
                        && lastState is bool b)
                        boolHandler.Invoke(key.Id, b);
                    else if (value is Action<bool?> boolHandler2)
                        boolHandler2.Invoke(lastState as bool?);
                    else if (value is Func<bool?, Task> boolHandler3)
                        boolHandler3.Invoke(lastState as bool?);
                    break;

                case PageAction.Resize:
                    var lastResize = lastState as (int w, int h, bool s)?;
                    if(lastResize.HasValue)
                    {
                        var (w, h, s) = lastResize.Value;
                        if (value is Action<int, int, bool> resizeHandler)
                            resizeHandler.Invoke(w, h, s);
                        else if (value is Action<int, int> resizeHandler2)
                            resizeHandler2.Invoke(w, h);
                        else if (value is Func<int, int, bool, Task> resizeHandler3)
                            _ = resizeHandler3.Invoke(w, h, s);
                    }

                    break;
            }
        }
    }



    // Specialized JS Invokables for the Bridge
    [JSInvokable] public void OnScrolled(string id, bool atBottom) => UpdateStateDebouncer(PageAction.Scroll, id, atBottom);
    [JSInvokable] public void OnResized(string id, int width, int height, bool isSmall) => UpdateStateDebouncer(PageAction.Resize, id, (w: width, h: height, s: isSmall));
    [JSInvokable] public void OnFocused(bool focused) => UpdateStateDebouncer(PageAction.Focus, "window", focused);
    [JSInvokable] public void OnVisibility(string visible) => UpdateStateDebouncer(PageAction.Visible, "window", visible);
    [JSInvokable] public void OnReconnected(string state) => UpdateStateDebouncer(PageAction.Visible, "window", state);
    [JSInvokable] public void OnPageEvent(string id, object? detail = null) => UpdateStateDebouncer(id.TryParse<PageAction>() ?? PageAction.Action, "window", detail);
    public void OnPageEvent(PageAction id, object? detail = null) => UpdateStateDebouncer(id, "window", detail);
    //[JSInvokable] public void OnStopped() => Form?.StopAsync();

    // 1. Generic GetState for type-safe access in C#
    public T? GetState<T>(PageAction action, string id)
    {
        if (_states.TryGetValue((action, id), out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }
    protected void UpdateStateDebouncer(PageAction action, string id, object? value)
    {
        TaskExtensions.Debounce(
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
    }


    public async ValueTask TriggerEvent(string eventName, object? detail = null)
    {
        await ModuleInitialize;
        OnPageEvent(eventName, detail);
        await Module.InvokeVoidAsync("dispatchEvent", eventName, detail);
    }


    public async ValueTask TriggerEvent(PageAction eventName, object? detail = null)
    {
        await ModuleInitialize;
        OnPageEvent(eventName, detail);
        await Module.InvokeVoidAsync("dispatchEvent", eventName.ToString(), detail);
    }

    public async ValueTask InitializeBackground(string mode, string canvas)
    {
        await ModuleInitialize;
        await Module.InvokeVoidAsync("initBackground", mode.ToString().ToLower(), canvas);
    }

    public async ValueTask Clipboard(string text)
    {
        await ModuleInitialize;
        await (Rendered.Runtime as IJSRuntime)!.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }

    // TODO: move this to mainloader classes along side SetTitle
    public void SetPageClasses(List<string> classes)
    {
        PageClasses = classes;
    }

    public void SetTheme(string? classes)
    {
        var newClass = "theme-" + (classes?.ToLowerInvariant() ?? string.Empty);
        Theme = newClass;
    }

    public void SetSidebar(string? classes)
    {
        Sidebar = classes;
    }

    public void SetBackground(string? classes)
    {
        var newClass = "background-" + (classes?.ToLowerInvariant() ?? string.Empty);
        Background = newClass;
    }

    private void SetBackground(AnimationMode? theme)
    {
        var newClass = "background-" + (theme?.ToString()?.ToLowerInvariant() ?? string.Empty);
        Background = newClass;
    }

    #endregion

}

public static class PageFormExtensions
{
    public static async Task SetSessionCookie(this IPageManager? Page, string name, string value, int days)
    {
        if (Page is not PageManager Cast) return;
        await Cast.ModuleInitialize;
        await Cast.Module.InvokeVoidAsync("setSessionCookie", name, value, days);
    }


    public static async Task<string?> GetSessionCookie(this IPageManager? Page, string name)
    {
        if (Page is not PageManager Cast) return null;
        await Cast.ModuleInitialize;
        return await Cast.Module.InvokeAsync<string>("getSessionCookie", name);
    }


    public static async Task SetPageTitle(this IPageManager? Page, string? title)
    {
        if (Page is not PageManager Cast) return;
        await Cast.ModuleInitialize;
        await Cast.Runtime!.InvokeVoidAsync("eval", "document.title = " + JsonSerializer.Serialize(title));
    }
}

