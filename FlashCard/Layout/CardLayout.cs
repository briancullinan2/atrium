using System;
using System.Collections.Generic;
using System.Text;

namespace FlashCard.Layout;

public class CardLayout : Presentation
{
    [Inject] public ITitleService? TitleService { get; set; } = null;
    [Parameter] public override string Class { get; set; } = "flash-card";
    [Parameter] public override string? ActiveClass { get; set; } = "animate-in";
    [Parameter] public override string? InactiveClass { get; set; } = "animate-out";

    protected override RenderFragment DefaultTitle => __builder =>
    {
        if (IsExpanded)
            TitleService?.UpdateTitle(Title); // showing one at a time with flipping in between
    };
}
