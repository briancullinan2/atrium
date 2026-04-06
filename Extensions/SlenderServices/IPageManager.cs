using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices
{

    public interface IPageManager : IAsyncDisposable
    {
        Task SetState(IComponent? state);
        Task<Dictionary<string, string?>?> RestoreState(IComponent component);
        Task SetError(Exception? error);
        Task<MarkupString> Copy(RenderFragment? _activeBody, IServiceProvider Services);

        // page data handling instead of built in MS uninspectable crap
        Dictionary<string, string?> State { get; set; }
        event Action<IComponent?>? OnStateChanged;
        event Action<Exception?>? OnErrorChanged;

        // page events
        Task RegisterAsync(string id);
        bool IsLocked(string id);
        Task ScrollToBottom(string id, bool smooth = true);
        Task<Dictionary<string, bool>> GetAllStatesAsync(string[]? ids = null);
        T? GetState<T>(PageAction action, string id);

        // special subscribers
        //Delegate? this[PageAction action, string id] { get; set; }


        BooleanProxy OnScroll { get; }
        event Action<int, int, bool> OnResize;
        event Func<int, int, bool, Task> OnResizeAsync;
        BooleanProxy OnFocus { get; }
        event Action<bool> OnVisible;
        event Action<string> OnReconnect;
        Delegate? this[PageAction action, string id] { get; set; }
        void Subscribe((PageAction Action, string Id) key, Delegate? value);

        Task<bool> IsAtBottomAsync(string id);
        Task ScrollSlightlyAsync(string id, int amount = 10);
        Task<string> GetLineHeightAsync(string? elementId = null);
        Task<int> GetLineHeightIntAsync(string? elementId = null);
        ValueTask InitializeBackground(string mode, string canvas);
        ValueTask Clipboard(string text);

        Task EnsureModuleLoaded();
        Task ModuleInitialize { get; }
        IJSObjectReference Module { get; }

        void OnFocused(bool focused);
        void OnScrolled(string id, bool atBottom);
        void OnResized(string id, int width, int height, bool isSmall);
        void OnVisibility(string visible);
        void OnReconnected(string state);
        void OnStopped();

        ValueTask TriggerEvent(string eventName, object? detail = null);
        bool IsReady { get; }
        int OffsetInMinutes { get; }
        Dictionary<string, string?> InFlight { get; }
        ConcurrentDictionary<string, string> ClassNames { get; }
        IJSRuntime Runtime { get; }
    }

    public enum PageAction
    {
        Scroll,
        Resize,
        Focus,
        Visible,
        Reconnect
    }
}
