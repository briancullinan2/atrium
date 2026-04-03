using Microsoft.AspNetCore.Components;

namespace RazorSharp.Services
{
    public interface IMenuService
    {
        Task SetMenu(RenderFragment? menu);
        event Action<RenderFragment?>? OnMenuChanged;

        Task SetHeader(bool? show);
        event Action<bool?>? OnHeaderChanged;
    }

    public class MenuService : IMenuService
    {

        public event Action<RenderFragment?>? OnMenuChanged;

        public async Task SetMenu(RenderFragment? menu)
        {
            OnMenuChanged?.Invoke(menu);
        }

        public event Action<bool?>? OnHeaderChanged;

        public async Task SetHeader(bool? show)
        {
            OnHeaderChanged?.Invoke(show);
        }
    }
}
