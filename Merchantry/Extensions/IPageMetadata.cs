
namespace Merchantry.Extensions;

public static partial class FlashCardExtensions
{

    public static async Task AnkiSelected(this IPageEvents Page, Tuple<PropertyMetadata, DataShared.ForeignEntity.File?, object?> args)
        => await Page.TriggerEvent(PageAction.AnkiSelected, args);

}
