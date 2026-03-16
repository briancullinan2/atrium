namespace FlashCard.Services
{
    public interface IChatService
    {
        Task<Tuple<bool?, string?>> PingService(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters);
        Task<List<ServicePreset>> ListPresets();
        Task<string?> SendMessage(string message);
        Task<bool?> IsWorking();
        event Action<bool?>? OnChatWorking;
        Task SetChatMode(bool chat);
        bool Chat { get; set; }
        event Action<bool>? OnChatChanged;
        Dictionary<DateTime, Tuple<bool, string>>? Recents { get; }
        event Action? OnChatMessage;
    }


    public class ServicePreset
    {
        public bool IsDefault { get; set; } = false;
        public bool IsPrevious { get; set; } = false;
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string DefaultModel { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ResponsePath { get; set; } = "";
        public List<DynamicParam> Params { get; set; } = new();
    }

    public class DynamicParam
    {
        public string Key { get; set; } = "";
        public string Type { get; set; } = "string";
        public string Value { get; set; } = "";
        public bool BoolValue { get; set; }
    }

}
