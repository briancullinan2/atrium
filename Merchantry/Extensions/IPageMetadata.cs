using Extensions.ForeignEntity;
using Extensions.SlenderServices;
using System;
using System.Threading.Tasks;

namespace Merchantry.Extensions;

public static partial class FlashCardExtensions
{

    public static async Task AnkiSelected(this IPageManager Page, Tuple<PropertyMetadata, FlashData.Entities.File?, object?> args)
        => await Page.TriggerEvent(PageAction.AnkiSelected, args);

}
