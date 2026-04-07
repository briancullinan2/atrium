using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices;

public static partial class PageExtensions
{
    

    public static async Task TurnOnStudyMode(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Study, true);

    public static async Task TurnOffStudyMode(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Study, false);


    public static async Task ToggleStudyMode(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Study, !Page.ClassNames.ContainsKey("study-mode"));


    public static async Task TriggerEvent<TEvent>(this IPageManager Page, TEvent _)
        => await Page.TriggerEvent(PageAction.Action, new
        {
            attemptedAt = DateTime.Now,
        });


    public static async Task TriggerAppStop(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.StopApp);


    public static async Task ScrollToElement(this IPageManager Page, string elementId)
        => await Page.TriggerEvent(PageAction.Scroll, new { Id = elementId });

    public static async Task ReconnectSocket(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Reconnect, null);

    public static async Task SetPageVisibility(this IPageManager Page, bool isVisible)
        => await Page.TriggerEvent(PageAction.Visible, isVisible);


    public static async Task AddNewCard(this IPageManager Page, Guid packId)
        => await Page.TriggerEvent(PageAction.AddCard, new { PackId = packId });

    public static async Task RemoveCard(this IPageManager Page, Guid cardId)
        => await Page.TriggerEvent(PageAction.RemoveCard, cardId);

    // Step Navigation
    public static async Task StepNext(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.NextStep, null);

    public static async Task StepBack(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.PreviousStep, null);



    public static async Task TriggerSave(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Save, DateTime.Now);

    public static async Task ToggleMenu(this IPageManager Page, string menuKey)
        => await Page.TriggerEvent(PageAction.ToggleMenu, menuKey);

    public static async Task EditObject(this IPageManager Page, object model)
        => await Page.TriggerEvent(PageAction.Edit, model);

    public static async Task HardDelete(this IPageManager Page, Guid id)
        => await Page.TriggerEvent(PageAction.Delete, id);

    public static async Task CopyToClipboard(this IPageManager Page, string text)
        => await Page.TriggerEvent(PageAction.Copy, text);


    public static async Task LogoutUser(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Login, false);

    public static async Task GrantPermission(this IPageManager Page, Guid userId, string permission)
        => await Page.TriggerEvent(PageAction.AddPermission, new { UserId = userId, Permission = permission });

    public static async Task RemoveUserFromGroup(this IPageManager Page, Guid userId, Guid groupId)
        => await Page.TriggerEvent(PageAction.RemoveGroup, new { UserId = userId, GroupId = groupId });


    public static async Task SendMessage(this IPageManager Page, string text)
        => await Page.TriggerEvent(PageAction.Send, text);

    public static async Task AddFormParameter(this IPageManager Page, string key, object value)
        => await Page.TriggerEvent(PageAction.AddParameter, new { Key = key, Value = value });

    public static async Task ExecuteForm(this IPageManager Page, MouseEventArgs _)
        => await Page.TriggerEvent(PageAction.Execute, null);


    public static async Task Upload(this IPageManager Page, InputFileChangeEventArgs args)
        => await Page.TriggerEvent(PageAction.Upload, args);

}
