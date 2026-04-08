namespace Extensions.SlenderServices;

// TODO: delete this in favor on [Display] attributes for titles and menu items
public class SyncedTitle : ComponentBase
{
    // 1. Replaces @inject
    [Inject] public ITitleService TitleService { get; set; } = default!;

    // 2. The Parameter stays the same
    [Parameter] public string? Title { get; set; }

    // 3. The Lifecycle method
    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrEmpty(Title))
        {
            await TitleService.UpdateTitle(Title);
        }
    }
}
