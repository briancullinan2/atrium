using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using RazorSharp.Extensions;
using RazorSharp.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace RazorSharp.Extensions
{
    public static class Callbacks
    {
        public static async Task TriggerEvent(this IComponent component, string eventId)
        {
            // You can even pass data back to the JS listener via the second parameter
            var scope = component.Service()?.CreateScope();
            var Page = scope?.ServiceProvider.GetRequiredService<IPageManager>();
            //var JS = component.Runtime();
            if (Page == null) return;
            await Page.TriggerEvent(eventId, new
            {
                attemptedAt = DateTime.Now,
                source = component.State()?.ToString()
            });
        }



        public static async Task TriggerAppStop(this IComponent component, MouseEventArgs mouse)
            => _ = TriggerEvent(component, "app-stop");

    }
}
