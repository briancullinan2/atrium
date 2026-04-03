using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.JSInterop;
using System.Text.Json;

namespace RazorSharp.Services
{
    public class PageManager(
        IFormFactor _form,
        ILoggerFactory _logger,
        IRenderStateProvider _rendered,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? _context = null
    )
        : PageManager(_form, _logger, _rendered, _context), IPageManager
    {

        public override async Task SetState(IComponent? state)
        {
            await base.SetState(state);
            throw new InvalidOperationException("This probably wont work from the web client.");
        }

        public override async Task<Dictionary<string, string?>?> RestoreState(IComponent component)
        {
            var deserializedState = await base.RestoreState(component);
            if (deserializedState == null) return null;
            FlashCard.Utilities.Extensions.JsonExtensions.ToProperties(component, deserializedState);
            return deserializedState;
        }
    }
}
