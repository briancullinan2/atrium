using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices;


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


    event Action<string, bool> OnScroll;
    event Action<int, int, bool> OnResize;
    event Func<int, int, bool, Task> OnResizeAsync;
    event Action<string, bool> OnFocus;
    event Action<bool> OnVisible;
    event Action<string> OnReconnect;

    //Delegate? this[PageAction action, string id] { get; set; }
    void Subscribe((PageAction Action, string Id) key, Delegate? value);
    void Unsubscribe((PageAction Action, string Id) key, Delegate? value);

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
    ValueTask TriggerEvent(PageAction id, object? detail = null);
    bool IsReady { get; }
    int OffsetInMinutes { get; }
    Dictionary<string, string?> InFlight { get; }
    ConcurrentDictionary<string, string> ClassNames { get; }
    IJSRuntime Runtime { get; }

    Task Redirect(string url);
}

public enum PageAction
{
    // undefined
    Action = 0,

    // page internals
    Scroll = 1,
    Resize = 2,
    Focus = 3,
    Visible = 4,
    Reconnect = 5,

    // course
    Study = 100,
    AddCard = 101,
    RemoveCard = 102,
    AddPack = 103,
    RemovePack = 104,
    AddCourse = 105,
    RemoveCourse = 106,
    NextStep = 107,
    PreviousStep = 108,


    // main layout events
    HideMenus = 200,
    Save = 201,
    Upload = 202,
    Add = 203,
    Edit = 211,
    Remove = 204, // soft removes
    Download = 205,
    Import = 206,
    Delete = 207, // hard remove
    ToggleMenu = 208,
    Clear = 209, // temporary lists
    StopApp = 210,
    Copy = 212, // copy and paste actions


    // content merchantry
    AnkiSelected = 300,

    // auth
    Login = 500,
    AddUser = 501,
    RemoveUser = 502,
    AddGroup = 503, 
    RemoveGroup = 504,
    AddPermission = 505, 
    RemovePermission = 506,
    AddProvider = 507,
    RemoveProvider = 508,
    SaveProvider = 509,

    // chat
    Send = 600,

    // parameter forms like chat installer
    AddParameter = 1000,
    RemoveParameter = 1001,
    Execute = 1002,



}
