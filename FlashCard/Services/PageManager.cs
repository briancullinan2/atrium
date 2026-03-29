using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using FlashCard.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Reflection;

namespace FlashCard.Services
{
    public interface IPageManager : IRenderStateProvider
    {
        Task SetState(IComponent? state);
        Task RestoreState(IComponent component);
        event Action<IComponent?>? OnStateChanged;
        event Action<Exception?>? OnErrorChanged;
        Task SetError(Exception? error);
        void NotifyRendered();
        Task<MarkupString> Copy(RenderFragment? _activeBody, IServiceProvider Services);
        Dictionary<string, string?> State { get; set; }
    }


    public class PageManager(ILoggerFactory Logger, IJSRuntime JS) : IPageManager, IRenderStateProvider
    {
        internal static ConcurrentQueue<(DateTime Created, Exception Exception)> Immediate { get; set; } = [];

        public Dictionary<string, string?> State { get; set; } = [];

        public IJSObjectReference? Module { get; private set; }

        public bool IsRendered { get => _renderTcs.Task.IsCompleted; private set => _renderTcs.TrySetResult(value); }

        public event Action<IComponent?>? OnStateChanged;
        public event Action<Exception?>? OnErrorChanged;

        private readonly TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // This is the task your LocalStore will 'Then' off of
        public event Action? OnRendered;

        // This is called by your MainLayout or Root component
        public void NotifyRendered()
        {
            if (!IsRendered)
            {
                IsRendered = true;
                OnRendered?.Invoke();
                //Rendered.IsRendered = true;
            }
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

        public virtual async Task RestoreState(IComponent component)
        {
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

    }
}
