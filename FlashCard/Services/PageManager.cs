using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Components;

namespace FlashCard.Services
{
    public interface IPageManager
    {
        Task SetState(IComponent? state);
        Task RestoreState(IComponent component);
        event Action<IComponent?>? OnStateChanged;
        event Action<Exception?>? OnErrorChanged;
        Task SetError(Exception? error);


        Dictionary<string, string?> State { get; set; }
    }


    public class PageManager : IPageManager
    {

        public Dictionary<string, string?> State { get; set; } = [];

        public bool IsWebClient { get; } = false;

        public PageManager()
        {
        }

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
    }
}
