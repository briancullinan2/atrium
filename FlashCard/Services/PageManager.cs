using DataLayer.Utilities.Extensions;
using FlashCard.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace FlashCard.Services
{
    public interface IPageManager
    {
        Task SetState(IComponent? state);
        Task RestoreState(IComponent component);
        event Action<IComponent?>? OnStateChanged;
        event Action<Exception?>? OnErrorChanged;
        Task SetError(Exception? error);

        Task<MarkupString> Copy(RenderFragment? _activeBody, IServiceProvider Services);
        Dictionary<string, string?> State { get; set; }
    }


    public class PageManager(ILoggerFactory LoggerFactory) : IPageManager
    {

        public Dictionary<string, string?> State { get; set; } = [];

        public bool IsWebClient { get; } = false;


        public event Action<IComponent?>? OnStateChanged;
        public event Action<Exception?>? OnErrorChanged;

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
            OnErrorChanged?.Invoke(error);
        }


        public async Task<MarkupString> Copy(RenderFragment? _activeBody, IServiceProvider Services)
        {
            var fragment = _activeBody;
            using var htmlRenderer = new HtmlRenderer(Services, LoggerFactory);

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
