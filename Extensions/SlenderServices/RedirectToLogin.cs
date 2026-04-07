

namespace Extensions.SlenderServices
{
    public interface ILogin : IComponent
    {
        string ReturnUrl { get; set; }
    }


    public class RedirectToLogin : ComponentBase
    {
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IPageManager PageManager { get; set; } = default!;
        [Inject] protected IFormFactor Form { get; set; } = default!;

        [CascadingParameter(Name = "IsStaticRender")]
        public bool IsStaticRender { get; set; }

        // TODO: holy shit, use route to component to track down components in other classes and convert them to their interfaces
        protected override void OnInitialized()
        {
            // 1. Critical for Headless/Proxy: Do not attempt to redirect during static rendering.
            if (IsStaticRender) return;

            //var currentUri = Navigation.ToAbsoluteUri(Navigation.Uri);

            _ = PageManager.Redirect(Navigation.Uri);
        }

        // Since this component only handles logic, we provide an empty render tree.
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            // No UI to render
        }
    }
}
