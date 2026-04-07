
namespace Interfacing.Services;


public interface IRenderState
{
    bool IsRendered { get; }
    IJSRuntime Runtime { get; }
    event Action OnRendered;
    event Action OnEmptied;
    void NotifyEmptied();
    void NotifyRendered(IJSRuntime Runtime);
    Task WaitForRender { get; }
}

public class RenderStateProvider : IRenderState
{

    private IJSRuntime? _runtime = null;
    public IJSRuntime Runtime
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
            if (IsRendered)
            {
                value?.Invoke();
            }
        }
        remove => _onRendered -= value;
    }
    public event Action? OnEmptied;



    private TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task WaitForRender => _renderTcs.Task;

    public bool IsRendered => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

    public void NotifyRendered(IJSRuntime runtime)
    {
        Runtime = runtime;
        // Fulfill the promise for everyone currently waiting
        _renderTcs.TrySetResult(true);
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
}
