using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using FlashCard.Services;
using System.Text.Json;

namespace WebClient.Services
{
    public class StateService : IJsonService
    {
        private IJSRuntime _runtime;
        public event Action<IComponent?>? OnStateChanged;

        public StateService(IJSRuntime JS) : base()
        {
            _runtime = JS;
        }

        public async Task SetState(IComponent? state)
        {

        }

        public async Task RestoreState(IComponent component)
        {
            var state = await _runtime.InvokeAsync<Dictionary<string, string?>>("eval",
@"Array.from(document.getElementsByTagName('input')).reduce((acc, input) => { 
    let key = input.id || input.name || 'unnamed';
    if(key.substring(0, 6) == 'state_')
        acc[key] = input.value; 
    return acc; 
}, {})");
            string? componentState = null;
            state.TryGetValue("state_" + component.GetType().Name.ToSafe(), out componentState);
            Console.WriteLine("Restoring: " + component.GetType().Name);
            if (componentState == null)
            {
                return;
            }
            var deserializedState = JsonSerializer.Deserialize<Dictionary<string, string?>>(componentState);
            Console.WriteLine("Deserializing: " + componentState);
            if (deserializedState == null)
            {
                return;
            }
            FlashCard.Utilities.Extensions.JsonExtensions.ToProperties(component, deserializedState);

        }
    }
}
