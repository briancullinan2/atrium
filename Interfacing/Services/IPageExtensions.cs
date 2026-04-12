

namespace Interfacing.Services;

public static partial class PageExtensions
{
    

    public static async Task TurnOnStudyMode(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.Study, true);

    public static async Task TurnOffStudyMode(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.Study, false);


    public static async Task ToggleStudyMode(this IPageEvents Page, bool studyOn)
        => await Page.TriggerEvent(PageAction.Study, studyOn);


    public static async Task TriggerEvent<TEvent>(this IPageEvents Page, TEvent _)
        => await Page.TriggerEvent(PageAction.Action, new
        {
            attemptedAt = DateTime.Now,
        });


    public static async Task TriggerAppStop(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.StopApp);


    public static async Task ScrollToElement(this IPageEvents Page, string elementId)
        => await Page.TriggerEvent(PageAction.Scroll, new { Id = elementId });

    public static async Task ReconnectSocket(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.Reconnect, null);

    public static async Task SetPageVisibility(this IPageEvents Page, bool isVisible)
        => await Page.TriggerEvent(PageAction.Visible, isVisible);


    public static async Task AddNewCard(this IPageEvents Page, Guid packId)
        => await Page.TriggerEvent(PageAction.AddCard, new { PackId = packId });

    public static async Task RemoveCard(this IPageEvents Page, Guid cardId)
        => await Page.TriggerEvent(PageAction.RemoveCard, cardId);

    // Step Navigation
    public static async Task StepNext(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.NextStep, null);

    public static async Task StepBack(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.PreviousStep, null);



    public static async Task TriggerSave(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.Save, DateTime.Now);

    public static async Task ToggleMenu(this IPageEvents Page, string menuKey)
        => await Page.TriggerEvent(PageAction.ToggleMenu, menuKey);

    public static async Task EditObject(this IPageEvents Page, object model)
        => await Page.TriggerEvent(PageAction.Edit, model);

    public static async Task HardDelete(this IPageEvents Page, Guid id)
        => await Page.TriggerEvent(PageAction.Delete, id);

    public static async Task CopyToClipboard(this IPageEvents Page, string text)
        => await Page.TriggerEvent(PageAction.Copy, text);


    public static async Task LogoutUser(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.Login, false);

    public static async Task GrantPermission(this IPageEvents Page, Guid userId, string permission)
        => await Page.TriggerEvent(PageAction.AddPermission, new { UserId = userId, Permission = permission });

    public static async Task RemoveUserFromGroup(this IPageEvents Page, Guid userId, Guid groupId)
        => await Page.TriggerEvent(PageAction.RemoveGroup, new { UserId = userId, GroupId = groupId });


    public static async Task SendMessage(this IPageEvents Page, string text)
        => await Page.TriggerEvent(PageAction.Send, text);

    public static async Task AddFormParameter(this IPageEvents Page, string key, object value)
        => await Page.TriggerEvent(PageAction.AddParameter, new { Key = key, Value = value });

    public static async Task ExecuteForm(this IPageEvents Page, EventArgs _)
        => await Page.TriggerEvent(PageAction.Execute, null);


    public static async Task Upload(this IPageEvents Page, EventArgs args)
        => await Page.TriggerEvent(PageAction.Upload, args);

}
