using FlashCard.Services;
using FlashCard.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlashCard.Utilities
{
    public static class Callbacks
    {
        public static async Task TriggerEvent(this IComponent component, MouseEventArgs mouse, string eventId)
        {
            // You can even pass data back to the JS listener via the second parameter
            var Page = component.Service()?.GetRequiredService<IPageManager>();
            var JS = component.Runtime();
            if (Page == null) return;
            await Page.TriggerEvent(eventId, new
            {
                attemptedAt = DateTime.Now,
                source = component.State()?.ToString()
            });
        }



        public static async Task TriggerAppStop(this IComponent component, MouseEventArgs mouse)
            => _ = TriggerEvent(component, mouse, "app-stop");

    }
}
