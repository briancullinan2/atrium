using Antlr4.Runtime;
using DataLayer.Entities;
using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using FlashCard.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Xml.Linq;
using static FlashCard.Services.ResizeProxy;

namespace FlashCard.Services
{

    public interface IPageManager : IAsyncDisposable
    {
        Task SetState(IComponent? state);
        Task<Dictionary<string, string?>?> RestoreState(IComponent component);
        Task SetError(Exception? error);
        Task<MarkupString> Copy(RenderFragment? _activeBody, IServiceProvider Services);
        Task SetSessionCookie(string name, string value, int days);

        // page data handling instead of built in MS uninspectable crap
        Dictionary<string, string?> State { get; set; }
        event Action<IComponent?>? OnStateChanged;
        event Action<Exception?>? OnErrorChanged;

        // page events
        Task RegisterAsync(string id);
        bool IsLocked(string id);
        Task EnsureBottomAsync(string id, bool force = false);
        Task<Dictionary<string, bool>> GetAllStatesAsync(string[]? ids = null);
        T? GetState<T>(PageAction action, string id);

        // special subscribers
        Action<string, object?>? this[PageAction action, string id] { get; set; }


        public BooleanProxy OnScroll { get; }
        public ResizeProxy OnResize { get; }
        public BooleanProxy OnFocus { get; }
        public StringProxy OnVisible { get; }
        public StringProxy OnReconnect { get; }

        Task<bool> IsAtBottomAsync(string id);
        Task ScrollSlightlyAsync(string id, int amount = 10);
        Task<string> GetLineHeightAsync(string? elementId = null);
        Task<int> GetLineHeightIntAsync(string? elementId = null);

        Task EnsureModuleLoaded();
        Task ModuleInitialize { get; }

        void OnFocused(string id, bool focused);
        void OnScrolled(string id, bool atBottom);
        void OnResized(string id, int width, int height, bool isSmall);
        void OnVisibility(string visible);
        void OnReconnected(string state);
        void OnStopped();

        ValueTask TriggerEvent(string eventName, object? detail = null);
    }

    public enum PageAction
    {
        Scroll,
        Resize,
        Focus,
        Visible,
        Reconnect
    }


    public class DelegateProxy<T>(PageManager parent, PageAction action)
        
    {
        // Usage: Page.OnScroll["Navlist"] += ...
        public Action<string, T?>? this[string id]
        {
            get
            {
                if (parent[action, id] is Action<string, T?> castion2)
                    return castion2;
                return null;
            }
            set
            {
                parent[action, id] = (s, o) => {
                    value?.Invoke(s, o?.GetType().Extends(typeof(T)) == true ? (T)o : default);
                };
            }
        }
    }

    public class StringProxy(PageManager parent, PageAction action)
        : DelegateProxy<string>(parent, action)
    {

    }

    public class BooleanProxy(PageManager parent, PageAction action)
        : DelegateProxy<bool>(parent, action)
    {

    }

    public class ResizeProxy(PageManager parent)
        : DelegateProxy<(int w, int h, bool s)>(parent, PageAction.Resize)
    {
    }


    public class PageManager : IPageManager
    {

        #region "Initialization"

        private TaskCompletionSource<bool> _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal static ConcurrentQueue<(DateTime Created, Exception Exception)> Immediate { get; set; } = [];

        public Dictionary<string, string?> State { get; set; } = [];

        public event Action<IComponent?>? OnStateChanged;
        public event Action<Exception?>? OnErrorChanged;

        readonly IFormFactor Form;
        readonly ILoggerFactory Logger;
        readonly IRenderStateProvider Rendered;
        readonly IConnectionStateProvider? Client;
        readonly IHttpContextAccessor? Context;

        public PageManager(
            IFormFactor _formFactor,
            ILoggerFactory _logger,
            IRenderStateProvider _rendered,
            IConnectionStateProvider? _client = null,
            Microsoft.AspNetCore.Http.IHttpContextAccessor? _context = null
        ) : base() {
            Form = _formFactor;
            Logger = _logger;
            Rendered = _rendered;
            Client = _client;
            Context = _context;
            Rendered.OnEmptied += NotifyEmptied;
            Rendered.OnRendered += NotifyRendered;
        }


        protected void NotifyEmptied() 
        {
            if (_restartRequired.Task.IsCompleted)
                _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        protected void NotifyRendered() => _ = EnsureModuleLoaded();


        public async ValueTask DisposeAsync()
        {
            Rendered.OnRendered -= NotifyEmptied;
            Rendered.OnEmptied -= NotifyEmptied;
            if (Module != null)
            {
                await Module.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }


        public Task ModuleInitialize { get => _restartRequired.Task; }


        private IJSObjectReference? _module = null;
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
                Module = await Rendered.Runtime.InvokeAsync<IJSObjectReference>("import", "/_content/FlashCard/page.js");
                var dotNetHelper = DotNetObjectReference.Create(this);
                _restartRequired.TrySetResult(true);
                await Module.InvokeVoidAsync("subscribePageEvents", dotNetHelper);
            }
            finally
            {
                // 4. Always release the lock in a finally block
                _loadLock.Release();
            }
        }

        #endregion


        #region "Page State"

        public async Task SetSessionCookie(string name, string value, int days)
        {
            await ModuleInitialize;
            await Module.InvokeVoidAsync("setSessionCookie", name, value, days);
        }



        public virtual async Task SetState(IComponent? state)
        {
            if (state == null)
            {
                return;
            }
            State[state.GetType().Name.ToSafe()] = Utilities.Extensions.JsonExtensions.ToSerialized(state);
            OnStateChanged?.Invoke(state);
        }

        public virtual async Task<Dictionary<string, string?>?> RestoreState(IComponent component)
        {
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




        public async Task EnsureBottomAsync(string id, bool force = false)
        {
            if (force || IsLocked(id))
            {
                await ModuleInitialize;
                await Module.InvokeVoidAsync("scrollToBottom", id);
            }
        }

        public async Task<Dictionary<string, bool>> GetAllStatesAsync(string[]? ids = null)
        {
            await ModuleInitialize;
            return await Module.InvokeAsync<Dictionary<string, bool>>("getScrollStates", ids);
        }

        private readonly Dictionary<(PageAction Action, string Id), object?> _states = [];

        // Tracks the actual multicast delegates per action/id pair
        private readonly Dictionary<(PageAction Action, string Id), Delegate> _events = [];


        public BooleanProxy OnScroll => new(this, PageAction.Scroll);
        public ResizeProxy OnResize => new(this);
        public BooleanProxy OnFocus => new(this, PageAction.Focus);
        public StringProxy OnVisible => new(this, PageAction.Visible);
        public StringProxy OnReconnect => new(this, PageAction.Reconnect);



        public Action<string, object?>? this[PageAction action, string id]
        {
            get => _events.TryGetValue((action, id), out var del) ? (Action<string, object?>)del : null;
            set => Subscribe((action, id), value);
        }

        private void Subscribe((PageAction Action, string Id) key, Delegate? value)
        {
            if (value == null)
            {
                _events.Remove(key);
                return;
            }

            _events[key] = value;

            // THE AUTO-FIRE ENGINE
            if (_states.TryGetValue(key, out var lastState))
            {
                // Pattern match the action to know how to 'Replay' the state
                switch (key.Action)
                {
                    case PageAction.Visible:
                    case PageAction.Reconnect:
                        if (value is Action<string, string> stateHandler 
                            && lastState is string state)
                            stateHandler.Invoke(key.Id, state);
                        break;
                    case PageAction.Scroll:
                    case PageAction.Focus:
                        if (value is Action<string, bool> boolHandler 
                            && lastState is bool b)
                            boolHandler.Invoke(key.Id, b);
                        break;

                    case PageAction.Resize:
                        if (value is Action<string, (int w, int h, bool s)> resizeHandler 
                            && lastState is (int w, int h, bool s))
                            resizeHandler.Invoke(key.Id, (w, h, s));
                        break;
                }
            }
        }



        // Specialized JS Invokables for the Bridge
        [JSInvokable] public void OnScrolled(string id, bool atBottom) => UpdateState(PageAction.Scroll, id, atBottom);
        [JSInvokable] public void OnResized(string id, int width, int height, bool isSmall) => UpdateState(PageAction.Resize, id, (w: width, h: height, s: isSmall));
        [JSInvokable] public void OnFocused(string id, bool focused) => UpdateState(PageAction.Focus, id, focused);
        [JSInvokable] public void OnVisibility(string visible) => UpdateState(PageAction.Visible, "window", visible);
        [JSInvokable] public void OnReconnected(string state) => UpdateState(PageAction.Visible, "window", state);
        [JSInvokable] public void OnStopped() => Form.StopAsync();

        // 1. Generic GetState for type-safe access in C#
        public T? GetState<T>(PageAction action, string id)
        {
            if (_states.TryGetValue((action, id), out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        // 2. Unified UpdateState that handles the "Replay" logic
        protected void UpdateState(PageAction action, string id, object value)
        {
            var key = (action, id);
            _states[key] = value;

            if (_events.TryGetValue(key, out var del))
            {
                // Route the invocation based on the action type
                switch (action)
                {
                    case PageAction.Visible:
                    case PageAction.Reconnect:
                        if (del is Action<string, string> stateHandler && value is string state)
                            stateHandler.Invoke(id, state);
                        break;
                    case PageAction.Scroll:
                    case PageAction.Focus:
                        if (del is Action<string, bool> boolHandler && value is bool b)
                            boolHandler.Invoke(id, b);
                        break;

                    case PageAction.Resize:
                        if (del is Action<string, int, int, bool> resizeHandler && value is (int w, int h, bool s))
                            resizeHandler.Invoke(id, w, h, s);
                        break;
                }
            }
        }


        public async ValueTask TriggerEvent(string eventName, object? detail = null)
        {
            await ModuleInitialize;
            await Module.InvokeVoidAsync("dispatchEvent", eventName, detail);
        }

        #endregion

    }


    public record ConnectionMetadata(
        string Id,
        DateTime Timestamp,
        string? Reason = null,
        Exception? Exception = null
    );

    public interface IConnectionStateProvider
    {
        event Action<bool, ConnectionMetadata>? OnConnectionDown;
        event Action<bool, ConnectionMetadata>? OnConnectionUp;
        bool IsConnected { get; }
        int ClientCount { get; }

        // Standardized reporting methods
        //Task OnConnectionUpAsync(ConnectionMetadata metadata, CancellationToken ct = default);
        //Task OnConnectionDownAsync(ConnectionMetadata metadata, CancellationToken ct = default);
    }




}
