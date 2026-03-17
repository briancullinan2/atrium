using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using FlashCard.Services;
using System.Text.Json;

namespace WebClient.Services
{
    public class StateService(IJSRuntime JS) : object(), IJsonService
    {
        private readonly IJSRuntime _runtime = JS;
        public event Action<IComponent?>? OnStateChanged;
        public bool IsWebClient { get; } = true;

        public async Task SetState(IComponent? state)
        {
            OnStateChanged?.Invoke(state);
            throw new InvalidOperationException("This probably wont work from the web client.");
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
            _ = state.TryGetValue("state_" + component.GetType().Name.ToSafe(), out string? componentState);
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
