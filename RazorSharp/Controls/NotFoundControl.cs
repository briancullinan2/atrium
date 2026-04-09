using Microsoft.AspNetCore.Authorization;


namespace RazorSharp.Controls;


[AllowAnonymous]
public class NotFoundControl : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder __builder)
    {
        // <div class="flash-card">
        __builder.OpenElement(0, "div");
        __builder.AddAttribute(1, "class", "flash-card");

        // <h3>Not Found</h3>
        __builder.OpenElement(2, "h3");
        __builder.AddContent(3, "Not Found");
        __builder.CloseElement();

        // <p>Sorry, the content you are looking for does not exist.</p>
        __builder.OpenElement(4, "p");
        __builder.AddContent(5, "Sorry, the content you are looking for does not exist.");
        __builder.CloseElement();

        __builder.CloseElement(); // Close div
    }
}
