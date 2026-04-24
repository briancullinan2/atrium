
using System.Dynamic;
using System.Linq.Expressions;
using System.Xml.Linq;

namespace Interfacing.Services;

internal interface IWebBrowser : IJsProxy
{
}



public class JsProxyInterceptor : DispatchProxy
{
    private IJsProxy? _proxy;

    public static T Create<T>(IJsProxy proxy)
    {
        object? proxyObject = Create<T, JsProxyInterceptor>();
        (proxyObject as JsProxyInterceptor)?._proxy = proxy;
        return proxyObject != null ? (T)proxyObject : default!;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        string name = targetMethod!.Name;

        // 1. Handle Property Getters (get_PropertyName)
        if (name.StartsWith("get_"))
        {
            string propName = name[4..];
            if(propName == "Item")
                return _proxy![(string)args![0]!];
            // Return the proxy for the next level in the chain
            return _proxy![propName];
        }

        // 2. Handle Property Setters (set_PropertyName)
        if (name.StartsWith("set_"))
        {
            string propName = name[4..];
            // Assign the value using the indexer we implemented earlier
            if (propName == "Item")
                _proxy![(string)args![0]!] = args![1];
            else
                _proxy![propName] = args![0];
            return null;
        }

        // 3. Handle Method Calls (e.g., addEventListener, querySelector)
        // We treat these as Dynamic Invocation
        return InvokeDynamicMethod(_proxy!, name, targetMethod.ReturnType, args);
    }

    private static object? InvokeDynamicMethod(IJsProxy dyn, string name, Type returnType, object?[]? args)
    {
        dyn.TryInvokeMember(name, returnType, args ?? [], out var result);
        return result;
    }

}


public interface IJsProxy
{
    // Allows dynamic access to JS properties via indexer: proxy["color"] = "red"
    object? this[string propertyName] { get; set; }
    bool TryInvokeMember(string Name, Type returnType, object?[]? args, out object? result);
    T As<T>();

    string? Path { get; }

}


#pragma warning disable IDE1006 // matches js
public interface IDocument : IObject, IJsProxy
{

    string innerHTML { get; set; }

    string title { get; set; }

    IElement querySelector(string sel);
    IElement getElementById(string id);
}

public interface IConsole : IJsProxy
{
    void warn(string message, object? obj = null);
    void log(string message, object? obj = null);
    void info(string message, object? obj = null);
    void error(string message, object? obj = null);
}
public interface IHistory : IJsProxy
{
    void pushState(object state, string title, string url);
    void back();
    void forward();
}


public interface INativeBridge : IJsProxy
{
    void postMessage(object? state);

}


public interface IMap<TKey, TValue> : IDictionary<TKey, TValue>, IJsProxy
{
    TValue? get(TKey key);
    IMap<TKey, TValue> set(TKey key, TValue? value);
}



public interface IDomStringMap
{
    // The indexer maps directly to how JS accesses it: dataset['atriumId']
    string? this[string key] { get; set; }
}
public interface IObjectStatic
{
    // Object.assign(target, ...sources)
    T assign<T>(T target, params object[] sources);

    // Object.keys(obj)
    IEnumerable<string> keys(object obj);

    // Object.values(obj)
    IEnumerable<object?> values(object obj);

    // Object.entries(obj)
    IEnumerable<KeyValuePair<string, object?>> entries(object obj);

    // Object.create(proto)
    IObject create(IObject? prototype);

    // Object.defineProperty(obj, prop, descriptor)
    void defineProperty(IObject obj, string prop, object descriptor);

    // Object.freeze(obj)
    void freeze(IObject obj);
}

public interface IObject : IJsProxy
{
    // Property Access
    object? get(string key);
    void set(string key, object? value);

    // Member Inspection
    bool hasOwnProperty(string key);
    bool hasProperty(string key); // Includes prototype chain

    // Prototype Link
    IPrototype prototype { get; set; }

    // Standard JS methods that objects "have"
    string toString();
    string toLocaleString();
    object valueOf();
}
public interface IPrototype : IJsProxy
{
    // The link to the next object in the chain
    IPrototype? parent { get; }

    // Methods common to all objects via Object.prototype
    bool isPrototypeOf(IObject instance);
    bool propertyIsEnumerable(string key);

    // The "Internal" lookup mechanism
    object? getFromChain(string key);
}

public interface IWindow : IUtility, IPluginManager, IJsProxy
{

    INativeBridge NativeBridge { get; } // android
    IWebKit webkit { get; } // ios/catalyst
    IChromeHostObjects chrome { get; } // windows
    IAtrium Atrium { get; set; }

    IObjectStatic Object { get; }
    INode Node { get; }

    object? eval(string? script);


    // Natural Browser Objects
    IDocument document { get; }
    IConsole console { get; }
    IStorage localStorage { get; }
    IStorage sessionStorage { get; }
    INavigator navigator { get; }
    ILocation location { get; }
    IPluginManager plugins { get; }
    IUtility utils { get; }


    IMap<int, IElement> AtriumRegistry { get; }
    int AtriumIdCounter { get; set; }
    int getAtriumId(INode element);
    int setAtriumId(INode element);
    int? parseInt(string? test);

    void addEventListener(string type, string dotnetCallbackNamespace);
    void addEventListener(string type, Expression<Action> dotnetCallbackNamespace);
    void addEventListener(string type, Expression<Action<IPopStateEvent>> dotnetCallbackNamespace);
    void addEventListener(string type, Expression<Action<IWindow>> dotnetCallbackNamespace);
    void postMessage(object? state, string eventNamespace);
    

    // Mapped Utility Functions
    void clear(string ns);
    void dispatchEvent(string name, object detail);
    void replace(string sel, string cont);
    void insert(string id, string cont);

    // Complex Mappers
    object? selectDom(string select, object? ctx = null);
    object? queryDom(string select, object? ctx = null);
    void Deconstruct(out IDocument document, out IConsole console);
}

public interface Node : INode
{
}


public interface INode : IObject, IJsProxy
{
    string nodeName { get; }
    int nodeType { get; }
    INode? parentNode { get; }
    INode[] childNodes { get; }

    void appendChild(INode child);
    void removeChild(INode child);
}

public interface IElement : INode, IJsProxy
{
    string tagName { get; }
    string innerText { get; set; }
    string className { get; set; }
    ICSSStyleDeclaration style { get; }
    void addEventListener(string type, object listener);
    void setAttribute(string name, string value);
    string getAttribute(string name);
    string id { get; set; }
    string innerHTML { get; set; }

    // Core DOM methods
    void addEventListener(string type, Action<object> listener);
    IElement querySelector(string selector);
    IElement[] querySelectorAll(string selector);

    IDomStringMap dataset { get; }
}

public interface ICSSStyleDeclaration : IJsProxy
{
    // Common properties to ensure type safety
    string backgroundColor { get; set; }
    string color { get; set; }
    string display { get; set; }
    string position { get; set; }
    string top { get; set; }
    string left { get; set; }
    string width { get; set; }
    string height { get; set; }
    string opacity { get; set; }
    string zIndex { get; set; }
    string transform { get; set; }
    string transition { get; set; }

    // Fallback for custom CSS variables or less common properties
    new object? this[string propertyName] { get; set; }
}

public interface IStorage : IObject, IJsProxy
{
    void setItem(string key, string value);
    string getItem(string key);
    void removeItem(string key);
    public void clear();
}

public interface INavigator : IObject, IJsProxy
{
    public string userAgent { get; }
    public bool onLine { get; }
}

public interface ILocation : IObject, IJsProxy
{
    public string href { get; set; }
    public string hostname { get; }
    public void reload();
}
public interface IPluginManager : IJsProxy
{
    void register(string path, object helper, string[] methods, bool serviceable);
    void unregister(string path);
}

public interface IUtility : IJsProxy
{
    public object walkTree(object select, object ctx, object evaluate);
    object evaluateDom(string select, object ctx);
}

public interface IPopStateEvent : IObject, IJsProxy
{
    // The state object passed to pushState()
    object? state { get; }

    // Standard event properties
    string type { get; }
    bool bubbles { get; }
}

public interface IBaseEvent : IJsProxy
{
    string type { get; }
    bool bubbles { get; }
    bool cancelable { get; }
    double timeStamp { get; }
}

// UI Events
public interface IUIEvent : IBaseEvent , IJsProxy
{
    int detail { get; }
}

// Mouse/Pointer Events
public interface IMouseEvent : IUIEvent , IJsProxy
{
    int clientX { get; }
    int clientY { get; }
    int screenX { get; }
    int screenY { get; }
    bool ctrlKey { get; }
    bool shiftKey { get; }
    int button { get; }
}

// Keyboard Events
public interface IKeyboardEvent : IUIEvent, IJsProxy
{
    string key { get; }
    string code { get; }
    bool repeat { get; }
    bool altKey { get; }
}

// Input/Form Events
public interface IInputEvent : IUIEvent, IJsProxy
{
    string data { get; }
    string inputType { get; }
}

// Window/Lifecycle Events
public interface IHashChangeEvent : IBaseEvent, IJsProxy
{
    string oldURL { get; }
    string newURL { get; }
}

public interface IStorageEvent : IBaseEvent, IJsProxy
{
    string key { get; }
    string? oldValue { get; }
    string? newValue { get; }
    string url { get; }
}

public interface IProgressEvent : IBaseEvent, IJsProxy
{
    long lengthComputable { get; }
    long loaded { get; }
    long total { get; }
}

public static class JSON
{
    public static string stringify(object? obj) => JsonSerializer.Serialize(obj);
    public static object? parse(string data) => InteropExtensions.MapToDotNet(data, "tempObj", null);
}


public interface IWebKit : IJsProxy
{
    // Allows you to inspect if the bridge is even available
    bool HasMessageHandler(string handlerName);

    // Gives you access to the message handler registry
    IMessageHandlerRegistry messageHandlers { get; }

    // Useful for debugging current webview process stats
    string GetProcessInfo();
}

public interface IMessageHandlerRegistry : IJsProxy
{
    // Allows dynamic lookup: window.webkit.messageHandlers['bridge']
    new IMessageHandler this[string name] { get; }


    // Returns an array of available handler names
    string[] GetAvailableHandlers();
}

public interface IMessageHandler : IObject, IJsProxy
{
    // The core 'postMessage' functionality
    // Note: We use object data here so your JSON serializer can handle complex types
    void postMessage(object data);

    // Metadata about the handler
    string HandlerName { get; }

    // Useful for tracking how many messages have been dispatched
    long MessageCount { get; }

    // Check if the handler is ready to receive
    bool IsConnected { get; }
}
public interface IChromeHostObjects : IObject, IJsProxy
{
    // If you inject your "Atrium" services here, they appear under window.chrome.webview.hostObjects
    // This allows JS to call C# methods as if they were local functions
    dynamic sync { get; }
    dynamic async { get; }

    // Useful for exploring what C# objects are currently exposed to the browser
    string[] GetExposedObjectNames();

    IChromeWebView webview { get; }
}


public interface IChromeWebView : IJsProxy
{
    // The core messaging method
    void postMessage(object message);

    // Subscribes to messages coming FROM C#
    void addEventListener(string type, Action<object> listener);
    void removeEventListener(string type, Action<object> listener);

    // Chromium-specific host info
    bool hostObjects { get; }

    // Allows checking if the bridge is currently initialized
    bool inFrame { get; }
}

// TODO: get this working on all natives, sans post message
public interface IAtrium
{
    void PostMessage(object message);
}


public interface IWebViewBridge
{
    Task<string?> ExecuteJsAsync(string script);
    void SetHtml(string html);
    IWindow window { get; }

    event Action<IWindow>? OnDocument;
    event Func<IWindow, Task>? OnDocumentAsync;
    event Action<string?>? OnMessage;
    event Func<string?, Task>? OnMessageAsync;
}

#pragma warning restore IDE1006 // matches js


public abstract class WebViewBase : IWebViewBridge
{
    public abstract IWindow window { get; }

    private bool _isDocumentLoaded;
    private readonly Lock _lock = new();

    event Action<IWindow>? InternalDocument;
    event Func<IWindow, Task>? InternalDocumentAsync;
    public event Action<IWindow>? OnDocument
    {
        add
        {
            lock (_lock)
            {
                InternalDocument += value;
                if (_isDocumentLoaded && window != null && value != null)
                {
                    value(window);
                }
            }
        }
        remove
        {
            InternalDocument -= value;
        }
    }
    public event Func<IWindow, Task>? OnDocumentAsync
    {
        add
        {
            lock (_lock)
            {
                InternalDocumentAsync += value;
                if (_isDocumentLoaded && window != null && value != null)
                {
                    value(window);
                }
            }
        }
        remove
        {
            InternalDocumentAsync -= value;
        }
    }



    event Action<string?>? InternalMessage;
    event Func<string?, Task>? InternalMessageAsync;
    public event Action<string?>? OnMessage
    {
        add
        {
            lock (_lock)
            {
                InternalMessage += value;
            }
        }
        remove
        {
            InternalMessage -= value;
        }
    }
    public event Func<string?, Task>? OnMessageAsync
    {
        add
        {
            lock (_lock)
            {
                InternalMessageAsync += value;
            }
        }
        remove
        {
            InternalMessageAsync -= value;
        }
    }


    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingTasks = new();

    // JS calls this when it has a result
    public void OnJsResponse(string correlationId, string data)
    {
        if (_pendingTasks.TryRemove(correlationId, out var tcs))
            tcs.SetResult(data);
    }

    // C# calls this to wait for a specific event
    public Task<string> WaitForEventAsync(string correlationId)
    {
        var tcs = new TaskCompletionSource<string>();
        _pendingTasks[correlationId] = tcs;
        return tcs.Task;
    }


    protected abstract Expression<Action<IWindow>> Callback { get; }

    protected Expression<Action<IWindow>> DomReadyScript
    {
        get => (window) => window.addEventListener("DOMContentLoaded", Callback);
    }

    protected string DomReadyWrapped
    {
        get => '(' + DomReadyScript.ToJS() + ")()";
    }


    public abstract Task<string?> ExecuteJsAsync(string script);

    public virtual async Task<T?> ExecuteJsAsync<T>(string script)
    {
        return (T)InteropExtensions.MapToDotNet(await ExecuteJsAsync(script), "tempObj", typeof(T));
    }
    public virtual async Task<T?> ExecuteJsAsync<T>(Expression script)
    {
        return (T)InteropExtensions.MapToDotNet(await ExecuteJsAsync(script.ToJS()), "tempObj", typeof(T));
    }


    public abstract void SetHtml(string html);

    protected virtual void OnMessaged(string? message)
    {
        if (message == "DOM_READY")
        {
            /*InvokeAsync*/
            Task.Run(async () =>
            {
                lock (_lock)
                {
                    _isDocumentLoaded = true;
                }

                InternalDocument?.Invoke(window);
                _ = InternalDocumentAsync?.Invoke(window);

            });
            return;
        }
        /*InvokeAsync*/
        Task.Run(async () =>
        {
            InternalMessage?.Invoke(message);
            InternalMessageAsync?.Invoke(message);
        });
    }


    private readonly CancellationTokenSource? _cts;

    public Task Task { get; private set; }
    public int ThreadId { get; private set; }
    public int CreateId { get; private set; }
    public SynchronizationContext Context { get; }

    public WebViewBase()
    {
        CreateId = Environment.CurrentManagedThreadId;
        Context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("UiDispatcher must be initialized on the UI thread.");

        _cts = new CancellationTokenSource();

        Task = System.Threading.Tasks.Task.Factory.StartNew(() =>
        {
            ThreadId = Environment.CurrentManagedThreadId;
            // This loop now handles both manual invokes AND await continuations
            _context.Run(_cts.Token);
        }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private readonly SingleThreadSynchronizationContext _context = new();
    public void InvokeAsync(Func<Task> action) => _context.PostTask(action);
    public void Stop() => _cts?.Cancel();
}



public class SingleThreadSynchronizationContext : SynchronizationContext
{
    private readonly BlockingCollection<Action> _queue = [];

    // This is the "RunLoop"
    public void Run(CancellationToken token)
    {
        SynchronizationContext.SetSynchronizationContext(this);
        foreach (var action in _queue.GetConsumingEnumerable(token))
        {
            action();
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
        => _queue.Add(() => d(state));

    // Helper for your InvokeAsync
    public void PostTask(Func<Task> action)
        => _queue.Add(async () => await action());
}




public static class InteropExtensions
{
    private static readonly Type JsProxyType;
    private static readonly MethodInfo CreateProxy;

    static InteropExtensions()
    {
        JsProxyType = Assembly.GetEntryAssembly()?.GetType("Atrium.Services.JsProxy")
            ?? throw new InvalidOperationException("Could not find Atrium.Services.JsProxy");
        CreateProxy = typeof(JsProxyInterceptor).GetMethod(nameof(JsProxyInterceptor.Create))
            ?? throw new InvalidOperationException("Could not find JsProxyInterceptor.Create");
    }

    public static T As<T>(this IJsProxy proxy)
    {
        // We use your DispatchProxy interceptor to satisfy the interface 
        // while routing all calls back to the original JsProxy
        return JsProxyInterceptor.Create<T>(proxy);
    }


    public static object? MapToDotNet(string? json, string? currentPath, Type? expectedType)
    {
        if (string.IsNullOrEmpty(json) || json == "null" || json == "undefined")
            return null;

        // Try to parse as JSON
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.String => root.GetString(),
            JsonValueKind.Number => root.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => root.EnumerateArray().Select(e => MapToDotNet(e.GetRawText(), currentPath, expectedType)).ToList(),

            // For Objects, we return a new Proxy so you can chain: proxy.myObj.someMethod()
            JsonValueKind.Object => expectedType != null
                ? CreateProxy.MakeGenericMethod(expectedType)
                    .Invoke(null, [Activator.CreateInstance(JsProxyType, [currentPath, expectedType])])
                : Activator.CreateInstance(JsProxyType, [currentPath, expectedType]),

            _ => json
        };
    }


    public static Func<Expression, bool> FILTER_WINDOW = a => a is not ParameterExpression param || param.Name != "window";


    public static string ToJS(this Expression? node) => node switch
    {
        null => "",

        // --- Core ---
        ConstantExpression c when c.Value == null => "null",
        ConstantExpression c => c.Value is string s ? $"'{s}'" : $"{c.Value}".ToLower(),
        ParameterExpression p => p.Name ?? "",

        // --- Logic & Math ---
        BinaryExpression b => $"{b.Left.ToJS()} {GetJsOperator(b.NodeType)} {b.Right.ToJS()}",
        UnaryExpression { NodeType: ExpressionType.Convert } u => u.Operand.ToJS(), // JS doesn't need explicit casts
        UnaryExpression u => $"{(u.NodeType == ExpressionType.Not ? "!" : u.NodeType == ExpressionType.Negate ? "-" : "")}{u.Operand.ToJS()}",

        // --- Object & Member Access ---
        MethodCallExpression m when m.Method.Name == "As" => m.Arguments[0].ToJS(),
        MethodCallExpression m when m.Method.Name == "get_Item" => $"{m.Object.ToJS()}['{m.Arguments[0].ToJS().Trim('\'')}']",

        // For assignments (if you handle set_Item)
        MethodCallExpression m when m.Method.Name == "ToString" => $"{m.Object.ToJS()}.toString({string.Join(", ", m.Arguments.Select(a => a.ToJS()))})",
        MethodCallExpression m when m.Method.Name == "set_Item" => $"{m.Object.ToJS()}['{m.Arguments[0].ToJS().Trim('\'')}'] = {m.Arguments[1].ToJS()}",
        MethodCallExpression m when m.Method.Name == "querySelector" => $"{m.Object.ToJS()}.querySelector({m.Arguments[0].ToJS()})",
        MethodCallExpression m when m.Method.Name == "querySelectorAll" => $"Array.from({m.Object.ToJS()}.querySelectorAll({m.Arguments[0].ToJS()}))",

        MethodCallExpression m when m.Object is MethodCallExpression compile 
            && compile.Method.Name == "Compile" && m.Method.Name == "Invoke"
            => $"{compile.Object.ToJS()}({string.Join(", ", m.Arguments.Where(FILTER_WINDOW).Select(a => a.ToJS()))})",
        MethodCallExpression m when m.Method.GetParameters().Any(p => p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0) 
            => $"{(m.Object != null ? (m.Object.ToJS() + ".") : "")}{m.Method.Name}({string.Join(", ", m.Arguments.Where(FILTER_WINDOW).SelectMany((a, I) 
                => m.Method.GetParameters().ElementAt(I).GetCustomAttributes<ParamArrayAttribute>().Any() 
                    && a is NewArrayExpression na ? na.Expressions.Select(e => e.ToJS()) : [a.ToJS()]))})",
        MethodCallExpression m when IsRootWindow(m.Object) => $"window.{m.Method.Name}({string.Join(", ", m.Arguments.Where(FILTER_WINDOW).Select(a => a.ToJS()))})",
        MethodCallExpression m when m.Object != null => $"{m.Object.ToJS()}.{m.Method.Name}({string.Join(", ", m.Arguments.Where(FILTER_WINDOW).Select(a => a.ToJS()))})",
        
        MethodCallExpression m => $"{m.Method.Name}({string.Join(", ", m.Arguments.Where(FILTER_WINDOW).Select(a => a.ToJS()))})",

        //MethodCallExpression m => $"{m.Method.Name}({string.Join(", ", m.Arguments.Select(a => a.ToJS()))})",
        NewExpression n when IsAnonymousType(n.Type) => $"({{ {string.Join(", ", (n.Members ?? []).Select((m, i) => $"{m.Name}: {n.Arguments[i].ToJS()}"))} }})",
        //    : $"new {ne.Type.Name}({string.Join(", ", ne.Arguments.Select(a => a.ToJS()))})",
        NewExpression n => $"({{ {string.Join(", ", n.Arguments.Select((a, i) => $"{n.Members![i].Name}: {a.ToJS()}"))} }})",
        MemberExpression m when m.Member.Name == "Value" && m.Expression?.Type.IsGenericType == true
            && m.Expression.Type.GetGenericTypeDefinition() == typeof(Nullable<>) => m.Expression.ToJS(),
        MemberExpression m when IsRootWindow(m) => $"window.{m.Member.Name}",
        MemberExpression m when !IsRootWindow(GetRootExpression(m)) 
            && !typeof(IJsProxy).IsAssignableFrom(m.Type)
            && GetRootExpression(m).NodeType != ExpressionType.Parameter /*&& m.IsClosure()*/ => m.Evaluate(),
        MemberExpression m when IsRootWindow(m.Expression) => $"window.{m.Member.Name}",
        MemberExpression m => $"{m.Expression.ToJS()}.{m.Member.Name}",
        //stupid chat bot MemberExpression m => m.Expression?.Type.IsAssignableTo(typeof(ICSSStyleDeclaration)) == true
        //    ? $"{m.Expression.ToJS()}.{m.Member.Name}" // Direct mapping
        //    : $"{m.Expression.ToJS()}.{m.Member.Name}",
        MemberInitExpression mi => $"({{ {string.Join(", ", mi.Bindings.Select(b => $"{b.Member.Name}: {((MemberAssignment)b).Expression.ToJS()}"))} }})",

        // --- Functions & Control ---
        LambdaExpression l => $"(({string.Join(", ", l.Parameters
            .Where(p => p.Name != "window") // hack to get Callback {get;} to work
            .Select(p => p.Name))}) => {l.Body.ToJS()})",
        ConditionalExpression c => $"{c.Test.ToJS()} ? {c.IfTrue.ToJS()} : {c.IfFalse.ToJS()}",
        InvocationExpression i => $"{i.Expression.ToJS()}({string.Join(", ", i.Arguments.Select(a => a.ToJS()))})",

        // --- Collections & Arrays ---
        NewArrayExpression na => $"[{string.Join(", ", na.Expressions.Select(e => e.ToJS()))}]",
        ListInitExpression li => $"[{string.Join(", ", li.Initializers.Select(i => i.Arguments[0].ToJS()))}]",

        // --- Type & Casting ---
        TypeBinaryExpression t => $"{t.Expression.ToJS()} instanceof {t.TypeOperand.Name}",

        // --- Fallback ---
        _ => throw new NotSupportedException($"Node type {node.NodeType} not transpiled.")
    };

    private static bool IsRootWindow(Expression? expr)
    {
        if (expr == null) return false;
        return expr is ConstantExpression { Value: IJsProxy proxy } && proxy.Path == "window"
            || expr is ParameterExpression { Type: Type proxy3 } && proxy3 == typeof(IWindow);
    }
    private static bool IsAnonymousType(Type type)
    {
        return Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)
            && type.Name.Contains("AnonymousType")
            && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"));
    }

    private static string Evaluate(this MemberExpression m)
    {
        var result = Expression.Lambda(m).Compile().DynamicInvoke();
        if (result is Expression expr)
            return expr.ToJS();
        else
            return JsonSerializer.Serialize(result);
    }

    private static bool IsClosure(this MemberExpression node)
    {
        var root = GetRootExpression(node);
        if (root is ConstantExpression constant && constant.Value != null)
        {
            var typeName = constant.Value.GetType().Name;
            // Matches the "BS" naming convention for C# closures
            return typeName.Contains("<>c__DisplayClass") || typeName.Contains("DisplayClass");
        }
        return false;
    }

    private static Expression GetRootExpression(this Expression node)
    {
        var ballsToTheWall = true;
        while (ballsToTheWall)
        {
            if (node is MemberExpression member)
            {
                if (member.Expression == null) return node;

                node = member.Expression;
            }
            else if (node is MethodCallExpression method)
            {
                if (method.Object == null) return node;

                node = method.Object;
            }
            else
                return node;
            //else
            //    throw new InvalidOperationException("Can't do that here: " + node);
        }
        return node;
    }


    private static string GetJsOperator(ExpressionType type) => type switch
    {
        ExpressionType.Add => "+",
        ExpressionType.Subtract => "-",
        ExpressionType.Multiply => "*",
        ExpressionType.Divide => "/",
        ExpressionType.Equal => "===",
        ExpressionType.NotEqual => "!==",
        ExpressionType.GreaterThan => ">",
        ExpressionType.LessThan => "<",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.AndAlso => "&&",
        ExpressionType.OrElse => "||",
        _ => throw new NotSupportedException($"Operator {type} not mapped.")
    };
}

