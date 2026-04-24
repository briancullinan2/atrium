
namespace Atrium.Services;

#if BROWSER
using System.Runtime.InteropServices.JavaScript;
#endif

#if WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

#if ANDROID
using Android.Webkit;
#endif

#if MACCATALYST || IOS
using WebKit;
using Foundation;
#endif

using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Text.Json;

using IWindow = Interfacing.Services.IWindow;

public class JsProxy(string _jsPath, Type? proxyType) : DynamicObject, IJsProxy
{
    protected string Path { get; } = _jsPath;

    public T As<T>()
    {
        return InteropExtensions.As<T>(this);
    }


    static JsProxy()
    {
        var assemblyName = new AssemblyName("Atrium.GeneratedProxies");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        CreateProxy = typeof(JsProxyInterceptor).GetMethod(nameof(JsProxyInterceptor.Create))
            ?? throw new InvalidOperationException("Could not find JsProxyInterceptor.Create");

    }

    public object? this[string propertyName]
    {
        get
        {
            VerifyThread(); // Enforcement gate
            var targetType = proxyType?.GetProperty(propertyName)?.PropertyType;
            var baseProxy = new JsProxy($"{Path}['{propertyName}']", targetType);
            return targetType != null
                ? CreateProxy.MakeGenericMethod(targetType).Invoke(null, [baseProxy]) as IJsProxy
                : (IJsProxy)baseProxy;
        }
        set
        {
            VerifyThread(); // Enforcement gate
            // Trigger the execution logic manually
            string script = $"{Path}['{propertyName}'] = {JsonSerializer.Serialize(value)};";
//#if !BROWSER
//            _core?.ExecuteJsAsync(script).Wait();
//#else
            _core?.ExecuteJsAsync(script);
//#endif
        }
    }

    readonly WebViewBridge? _core = App.Bridge;

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        VerifyThread(); // Enforcement gate

        // 1. Construct the target path (e.g., "window.document.body.className")
        string targetPath = $"{Path}.{binder.Name}";

        // 2. Serialize the value to a JSON string to ensure valid JS syntax 
        // (e.g., "red" -> "\"red\"", true -> "true", 123 -> "123")
        string jsValue = JsonSerializer.Serialize(value);

        // 3. Construct the assignment script
        // This executes the assignment in the browser and returns the new value
        string script = $"{targetPath} = {jsValue};";

#if false
        string script = $@"
        if ('{binder.Name}' in {Path}) {{ 
            {targetPath} = {jsValue}; 
            'success'; 
        }} else {{ 
            'error'; 
        }}";
#endif

        // 4. Execute synchronously on the worker thread
        // We don't necessarily need the result, but we await to ensure completion
        var task = _core?.ExecuteJsAsync(script);
        //task?.Wait();

        return true; // DynamicObject expects true if the operation succeeded
    }


    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        VerifyThread();
        // Path construction: e.g., "window.document" + ".body"
        string newPath = $"{Path}.{binder.Name}";

        // Return a new proxy for the child property
        var baseProxy = new JsProxy(newPath, binder.ReturnType);
        result = CreateProxy.MakeGenericMethod(binder.ReturnType).Invoke(null, [baseProxy]);
        return true;
    }

    public override bool TryConvert(ConvertBinder binder, out object? result)
    {
        VerifyThread();
        // Now that the user is trying to USE the value, we force an execution
        var task = _core?.ExecuteJsAsync(Path);
        task?.Wait(); // Blocking because TryConvert is synchronous

        var json = task?.Result?.ToString();
        result = InteropExtensions.MapToDotNet(json, Path, binder.ReturnType);
        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        return TryInvokeMember(binder.Name, binder.ReturnType, args, out result);
    }

    public bool TryInvokeMember(string Name, Type returnType, object?[]? args, out object? result)
    {
        VerifyThread();
        // Serialize arguments for JS
        string jsArgs = string.Join(",", args?.Select(o => o is Expression expr ? expr.ToJS() : JsonSerializer.Serialize(o)) ?? []);
        string script = $"{Path}.{Name}({jsArgs})";

        // Execute and get the JSON string back
        var task = _core?.ExecuteJsAsync(script);
        task?.Wait();

        // Map the result back to C#
        result = InteropExtensions.MapToDotNet(task?.Result?.ToString(), $"{Path}.{Name}", returnType);
        return true;
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        VerifyThread(); // Enforcement gate
        // Assume the index is a string (e.g., proxy["color"])
        string propertyName = (string)indexes[0];

        // You can either create a nested proxy or perform an immediate execution
        // To match your current flow, we return a new proxy for the property:
        var baseProxy = new JsProxy($"{Path}['{propertyName}']", binder.ReturnType);
        result = CreateProxy.MakeGenericMethod(binder.ReturnType).Invoke(null, [baseProxy]);
        return true;
    }

    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? result)
    {
        VerifyThread(); // Enforcement gate
        string propertyName = (string)indexes[0];
        object value = indexes[1];

        // Delegate to the same execution logic as SetMember
        string script = $"{Path}['{propertyName}'] = {JsonSerializer.Serialize(value)};";
        _core?.ExecuteJsAsync(script); //.Wait();

        return true;
    }



#if false
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        string name = targetMethod.Name;

        // Handle Getters
        if (name.StartsWith("get_"))
        {
            TryGetMember(new GetMemberBinder(name.Substring(4)), out var result);
            return result;
        }
        // Handle Setters
        if (name.StartsWith("set_"))
        {
            TrySetMember(new SetMemberBinder(name.Substring(4)), args[0]);
            return null;
        }

        // Handle Methods
        TryInvokeMember(new InvokeMemberBinder(name), args ?? [], out var result);
        return result;
    }
#endif



    public static void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != App.Bridge?.ThreadId)
        {
            throw new InvalidOperationException(
                $"Thread Violation: JsProxy must be accessed from the worker thread (ID: {App.Bridge?.ThreadId}). " +
                $"Current thread is (ID: {Environment.CurrentManagedThreadId}).");
        }
    }



    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly MethodInfo CreateProxy;



#if false
    public static T Create<T>(string path) where T : class
    {
        var interfaceType = typeof(T);
        var typeName = $"{interfaceType.Name}_Generated_{Guid.NewGuid():N}";

        var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public, typeof(JsProxy));

        // Define Constructor: public Document(WebViewBridge bridge, string path) : base(bridge, path) { }
        var ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
            [typeof(string)]);
        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, typeof(JsProxy).GetConstructor([typeof(string)])!);
        il.Emit(OpCodes.Ret);

        // Implement interface methods
        foreach (var method in interfaceType.GetMethods())
        {
            var methodBuilder = typeBuilder.DefineMethod(method.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                method.ReturnType,
                [.. method.GetParameters().Select(p => p.ParameterType)]);

            var mIL = methodBuilder.GetILGenerator();
            // Route call: ((dynamic)this).MethodName(args)
            mIL.Emit(OpCodes.Ldarg_0);
            // Push arguments... (simplified for brevity)
            // ... (Logic to push array of args to TryInvokeMember)
            mIL.Emit(OpCodes.Callvirt, typeof(DynamicObject).GetMethod(nameof(TryInvokeMember))!);
            mIL.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder, method);
        }

        var generatedType = typeBuilder.CreateType();
        return (T)Activator.CreateInstance(generatedType!, path)!;
    }

#endif

}


public partial class WebViewBridge : WebViewBase
{
    Interfacing.Services.IWindow? _window = null;



    public override Interfacing.Services.IWindow window
    {
        get
        {
            if (_window == null)
            {
                var baseProxy = new JsProxy("window", typeof(IWindow));
                _window = JsProxyInterceptor.Create<Interfacing.Services.IWindow>(baseProxy);
            }
            return _window;
        }
    }


    public static void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != App.Bridge?.CreateId)
        {
            throw new InvalidOperationException(
                $"Thread Violation: WebViewBridge must be accessed from the worker thread (ID: {App.Bridge?.CreateId}). " +
                $"Current thread is (ID: {Environment.CurrentManagedThreadId}).");
        }
    }


}


#if BROWSER

public partial class WebViewBridge : WebViewBase, IWebViewBridge
{
    // Import the native browser 'eval' function
    [JSImport("globalThis.eval")]
    internal static partial string Eval(string script);

    public WebViewBridge()
    {
        
    }

    public override async Task<string?> ExecuteJsAsync(string script)
    {
        VerifyThread(); // Enforcement gate
        // Executes script at the lowest level of the JS runtime
        var jsonResult = Eval(script);
        return jsonResult;
    }

    [JSExport]
    public static void PostMessage(string? message) => App.Bridge?.OnMessaged(message);

    public override void SetHtml(string html)
    {
        string script = $@"
        document.open();
        document.write({JsonSerializer.Serialize(html)});
        document.close();
        {DomReadyScript}"; // Re-attach your bridge listeners!
        _ = ExecuteJsAsync(script);
    }


}

#elif WINDOWS

public partial class WebViewBridge : WebViewBase, IWebViewBridge
{
    private readonly CoreWebView2 core;
    protected override string Callback { get; } = "window.chrome.webview.postMessage";

    public WebViewBridge(CoreWebView2 _core)
    {
        core = _core;
        core.Settings.IsWebMessageEnabled = true;
#if DEBUG
        core.Settings.AreDevToolsEnabled = true;
#endif

        core.WebMessageReceived += (s2, e2) => OnMessaged(e2.TryGetWebMessageAsString());
        _ = core.AddScriptToExecuteOnDocumentCreatedAsync(DomReadyScript);

        //core.DOMContentLoaded += async (s3, e3) =>
        //{
        //  await s3.ExecuteScriptAsync($"document.innerText = {JsonSerializer.Serialize(Body)};");
        //};

    }


    public override Task<string?> ExecuteJsAsync(string script)
    {
        TaskCompletionSource<string?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Context.Post(async _ =>
        {
            try
            {
                var result = await core.ExecuteScriptWithResultAsync(script);
                tcs.SetResult(result.ResultAsJson);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex); // Crucial: don't swallow errors on the UI thread
            }
        }, null);
        return tcs.Task;
    }


    public override void SetHtml(string html) => core.NavigateToString(html);

}




#elif MACCATALYST || IOS
public partial class WebViewBridge : WebViewBase, IWebViewBridge
{
    private readonly WKWebView webView;
    private readonly ScriptHandler handler;

    protected override string Callback { get; } = "window.webkit.messageHandlers.bridge.postMessage";

    public class ScriptHandler(Action<string> callback) : NSObject, IWKScriptMessageHandler
    {
        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            // Extract the message body (usually a string or dictionary)
            string content = message.Body.ToString() ?? string.Empty;
            callback(content);
        }
    }


   public WebViewBridge(WebKit.WKWebView _webView)
    {
        webView = _webView;
        handler = new ScriptHandler(OnMessaged);
    
        // 1. Register the handler
        webView.Configuration.UserContentController.AddScriptMessageHandler(handler, "bridge");
    
        // 3. Create the UserScript
        var userScript = new WKUserScript(
            new NSString(DomReadyScript), 
            WKUserScriptInjectionTime.AtDocumentEnd, // Injects after DOM is built
            true // For all frames
        );
    
        // 4. Add it to the controller
        webView.Configuration.UserContentController.AddUserScript(userScript);
    }


    public override Task<string?> ExecuteJsAsync(string script)
    {
        //VerifyThread(); // Enforcement gate
        // Wrap the script to return JSON so we can deserialize it consistently
        var wrappedScript = $"JSON.stringify({script})";
        TaskCompletionSource<string?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Context.Post(async _ =>
        {
            try
            {

                var result = await webView.EvaluateJavaScriptAsync(wrappedScript);

                if (result is NSString nsStr)
                    tcs.SetResult(nsStr.ToString());
                else
                    tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex); // Crucial: don't swallow errors on the UI thread
            }
        }, null);
        return tcs.Task;
    }


    public override void SetHtml(string html) => webView.LoadHtmlString(html, null!);
}




#elif ANDROID

public partial class WebViewBridge : WebViewBase, IWebViewBridge
{
    private readonly Android.Webkit.WebView _webView;
    public class JsBridge(Action<string> callback) : Java.Lang.Object
    {
        [Android.Webkit.JavascriptInterface]
        [Java.Interop.Export("postMessage")]
        public void PostMessage(string message) => callback(message);
    }
    public WebViewBridge(Android.Webkit.WebView webView)
    {
        _webView = webView;
        _webView.Settings.JavaScriptEnabled = true;

        // Bind your native bridge interface (remains persistent)
        _webView.AddJavascriptInterface(new JsBridge(OnMessaged), "NativeBridge");

        // Assign the client to handle script re-injection automatically
        _webView.SetWebViewClient(new BridgeWebViewClient(DomReadyScript));
#if DEBUG
        Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
#endif
    }

    
    public override Task<string?> ExecuteJsAsync(string script)
    {
        //VerifyThread(); // Enforcement gate
        var tcs = new TaskCompletionSource<string?>();
        Context.Post(async _ =>
        {
            try
            {
                _webView.EvaluateJavascript(script, new JsCallback(tcs));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex); // Crucial: don't swallow errors on the UI thread
            }
        }, null);
        return tcs.Task;
    }


    public override void SetHtml(string html)
    {
        // LoadDataWithBaseURL is the standard way to load HTML strings on Android
        _webView.LoadDataWithBaseURL(null, html, "text/html", "UTF-8", null);
    }

    // Helper class to handle the Android JS callback
    private class JsCallback(TaskCompletionSource<string?> tcs) : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value)
        {
            tcs.TrySetResult(value?.ToString() ?? string.Empty);
        }
    }

}

public class BridgeWebViewClient(string initScript) : Android.Webkit.WebViewClient
{
    public override void OnPageFinished(Android.Webkit.WebView? view, string? url)
    {
        base.OnPageFinished(view, url);
        
        // This re-runs the bridge injection every time a page finishes loading
        view?.EvaluateJavascript(initScript, null);
    }
}


#endif
