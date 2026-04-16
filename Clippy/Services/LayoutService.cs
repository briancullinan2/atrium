using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Clippy.Services;

public class LayoutService : IHasLayout
{
    public static Delegate ShowLayout => ShouldShow;

    public static Delegate LayoutInsert => MainLayoutInsert;

    public static bool ShouldShow(IChatService Chat) => Chat.Chat;

    public static RenderFragment MainLayoutInsert(IChatService Chat)
    {
        if (Chat.Chat == true)
        {
            return __builder =>
            {
                // <div class="chat-box">
                __builder.OpenElement(0, "div");
                __builder.AddAttribute(1, "class", "chat-box");

                // <input type="text" ... />
                __builder.OpenElement(2, "input");
                __builder.AddAttribute(3, "type", "text");
                __builder.AddAttribute(4, "placeholder", "Type your message here...");

                // @onkeydown="HandleKeyDown"
                __builder.AddAttribute(5, "onkeydown", Chat.HandleKeyDown);

                // @bind="ChatMessage" (The "value" side)
                __builder.AddAttribute(6, "value", BindConverter.FormatValue(Chat.ChatMessage));

                // @bind:event="oninput" (The "change" side)
                __builder.AddAttribute(7, "oninput", EventCallback.Factory.CreateBinder(Chat, __value => Chat.ChatMessage = __value, Chat.ChatMessage ?? string.Empty));

                __builder.CloseElement(); // Close input

                // <button class="cta" @onclick="SendMessage">Send</button>
                __builder.OpenElement(8, "button");
                __builder.AddAttribute(9, "class", "cta");
                __builder.AddAttribute(10, "onclick", Chat.SendMessage);
                __builder.AddContent(11, "Send");
                __builder.CloseElement(); // Close button

                __builder.CloseElement(); // Close div
            };
        }
        else
        {
            return _ => { };
        }
    }

}



public static class EventExtensions
{



    internal static void SendMessage(this IChatService Chat, MouseEventArgs? args)
    {
        if (string.IsNullOrWhiteSpace(Chat.ChatMessage))
        {
            Chat.ChatMessage = "";
            return;
        }
        _ = Chat.SendMessage(Chat.ChatMessage ?? "");
        Chat.ChatMessage = "";
        // No need to InvokeAsync(StateHasChanged) because the SendMessage event handler will invoke it
    }


    internal static async Task HandleKeyDown(this IChatService Chat, KeyboardEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Chat.ChatMessage))
        {
            Chat.ChatMessage = "";
            return;
        }
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            _ = Chat.SendMessage(Chat.ChatMessage ?? "");
            Chat.ChatMessage = "";
        }
    }

}
