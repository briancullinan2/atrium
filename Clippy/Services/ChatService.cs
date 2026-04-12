using System.Diagnostics.CodeAnalysis;

namespace Clippy.Services;

public partial class ChatService(HttpClient Http, IHasClass Classy) : IChatService
{

    public bool Chat { get; set; } = false;

    // set the current message on the scoped for autocomplete feedback mid stream
    public string? ChatMessage { get; set; }

    public async Task SetChatMode(bool chat)
    {
        Chat = chat;
        OnChatChanged?.Invoke(chat);
    }


    public static Dictionary<string, Dictionary<DateTime, Tuple<bool, string>>>? AllRecents { get; set; } = [];
    public Dictionary<DateTime, Tuple<bool, string>>? Recents
    {
        get
        {
            if (AllRecents?.TryGetValue("", out var _recents) == true) return _recents;
            var newRecent = new Dictionary<DateTime, Tuple<bool, string>>();
            _ = (AllRecents?[""] = newRecent);
            return newRecent;
        }
    }

    [AllowAnonymous]
    public async Task<Tuple<bool, string>?> TryChat(object? settings = null)
    {
        try
        {
            var service = settings as ServicePreset ?? throw new InvalidOperationException("Failed to render service preset");

            var json = await ExecutePost(Http, "", PingMessage, service);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json ?? "");

            bool result = parsed?.TryGetValue("response", out var response) == true
                && string.Equals(response?.ToString(), "Supercalifragilisticexpialidocious", StringComparison.InvariantCultureIgnoreCase);

            if(result == true)
            {
                SaveWorkingSettings(service.Url, service.DefaultModel, service.ApiKey, service.ResponsePath, service.Parameters);
            }
            if (result) Classy.ClassNames.Add("chatting");
            else Classy.ClassNames.Remove("chatting");
            OnChatWorking?.Invoke(result);
            return Tuple.Create(result, json ?? string.Empty);
        }
        catch (Exception ex) {
            return Tuple.Create(false, ex.Message);
        }
    }



    [RequiresUnreferencedCode("serializing message data")]
    public class RecentModel
    {
        public DateTime Date { get; set; }
        public string? Role { get; set; }
        public string? Content { get; set; }
    }


    [AllowAnonymous]
    public async Task<string?> SendMessage(string message)
    {
        Recents?.Add(DateTime.Now, new Tuple<bool, string>(false, message));
        OnChatMessage?.Invoke();

        var result = await StandardResponse(Http, "", message);

        Recents?.Add(DateTime.Now + TimeSpan.FromMilliseconds(1), new Tuple<bool, string>(true, result ?? ""));
        OnChatMessage?.Invoke();
        return result;
    }


    private readonly SemaphoreSlim _gate = new(1, 1);

    public event Action<bool?>? OnChatWorking;
    public event Action<bool>? OnChatChanged;
    public event Action? OnChatMessage;

    private Task<Tuple<bool, string>?>? _pingTask;
    private Tuple<bool, string>? recentPing;

    public async Task<bool?> IsWorking()
    {
        _pingTask = TaskExtensions.Debounce<ServicePreset, Tuple<bool, string>>(TryChat, 1000)
            .Then(async r => (recentPing = r));

        return recentPing?.Item1;
    }
    



    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _clientTokens = new();


    
    public async Task<List<object>> ListPresets()
    {
        return [..GetPresets().Cast<object>()];
    }


    [AllowAnonymous]
    public static List<ServicePreset> GetPresets()
    {
        if (Settings == null) return [.. ServicePresets.Predefined];

        // 1. Create a lookup table (O(n) instead of repeated O(n) inside the loop)
        var predefinedMap = ServicePresets.Predefined.ToDictionary(p => p.Url, StringComparer.OrdinalIgnoreCase);

        // 2. Project settings with fallback to lookup
        var userPresets = Settings.Select(s => {
            predefinedMap.TryGetValue(s.Url, out var pre);
            return new ServicePreset
            {
                Url = s.Url,
                IsDefault = s.IsDefault,
                Name = string.IsNullOrWhiteSpace(s.Name) ? pre?.Name ?? "" : s.Name,
                ApiKey = pre?.ApiKey ?? "",
                IsPrevious = s.IsPrevious,
                DefaultModel = s.DefaultModel,
                Parameters = s.Parameters,
                ResponsePath = s.ResponsePath,
            };
        });

        // 3. Add predefined ones that aren't in settings
        var existingUrls = new HashSet<string>(Settings.Select(s => s.Url), StringComparer.OrdinalIgnoreCase);
        var remaining = ServicePresets.Predefined.Where(p => !existingUrls.Contains(p.Url));

        return [.. userPresets, .. remaining];
    }


    /*
    public RenderFragment? MainLayoutInsert(IChatService service)
    {
        // If chat isn't enabled, return null so nothing renders
        if (service.Chat != true) return null;

        return __builder =>
        {

            // <div class="chat-box">
            __builder.OpenElement(1, "div");
            __builder.AddAttribute(2, "class", "chat-box");

            // <input type="text" placeholder="..." />
            __builder.OpenElement(3, "input");
            __builder.AddAttribute(4, "type", "text");
            __builder.AddAttribute(5, "placeholder", "Type your message here...");

            // Manual @bind:value
            __builder.AddAttribute(6, "value", ChatMessage);

            // Manual @bind:event="oninput"
            __builder.AddAttribute(7, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(
                this,
                args => ChatMessage = args.Value?.ToString())
            );

            // @onkeydown="HandleKeyDown"
            __builder.AddAttribute(8, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(
                this,
                HandleKeyDown)
            );

            __builder.CloseElement(); // Close input

            // <button class="cta" @onclick="...">Send</button>
            __builder.OpenElement(9, "button");
            __builder.AddAttribute(10, "class", "cta");
            __builder.AddAttribute(11, "onclick", EventCallback.Factory.Create<MouseEventArgs>(
                this,
                () => {
                    _ = SendMessage(ChatMessage ?? "");
                    ChatMessage = "";
                })
            );
            __builder.AddContent(12, "Send");
            __builder.CloseElement(); // Close button

            __builder.CloseElement(); // Close div
        };
    }
    */


}
