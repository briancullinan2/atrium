
namespace RazorSharp.Layout;

public class Presentation : ComponentBase
{
    [Parameter] public RenderFragment? TitleContent { get; set; }
    [Parameter] public virtual string Class { get; set; } = "accordion-item";
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool IsExpanded { get; set; }

    [Parameter] public virtual string? ActiveClass { get; set; } = "header-active";
    [Parameter] public virtual string? InactiveClass { get; set; } = "inactive";

    public virtual Type? DefaultWrapper { get 
            => this.Parent() is IHasAccordion 
            ? typeof(AccordionSection) : typeof(Presentation);
    }

    [Parameter] public Type? Wrapper { get; set; }

    public void Toggle(MouseEventArgs mouse)
    {
        IsExpanded = !IsExpanded;
        InvokeAsync(StateHasChanged);
    }

    // The manual "Razor" logic
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // <div class="...">
        builder.OpenElement(0, "article");
        string combinedClass = $"{(IsExpanded ? ActiveClass : InactiveClass)} {Class}";
        builder.AddAttribute(1, "class", combinedClass);

        // @(TitleContent ?? DefaultTitle)
        builder.AddContent(2, TitleContent ?? DefaultTitle);

        // @ChildContent
        builder.AddContent(3, ChildContent);

        builder.CloseElement(); // </div>
    }

    protected virtual RenderFragment DefaultTitle => __builder =>
    {
        __builder.OpenElement(0, "h4");
        // Using a hypothetical .ToSafe() extension as per your snippet
        __builder.AddAttribute(1, "name", Title.ToSafe()); 
        __builder.AddAttribute(2, "onclick", Toggle);
        __builder.AddAttribute(3, "style", "cursor: pointer;");

        // <span>@Title</span>
        __builder.OpenElement(4, "span");
        __builder.AddContent(5, Title);
        __builder.CloseElement();

        // <i class="bi ..."></i>
        __builder.OpenElement(6, "i");
        string iconClass = $"bi {(IsExpanded ? "bi-chevron-up" : "bi-chevron-down")} text-muted";
        __builder.AddAttribute(7, "class", iconClass);
        __builder.CloseElement();

        __builder.CloseElement(); // </h4>
    };
}


public class AccordionSection : Presentation
{

}
