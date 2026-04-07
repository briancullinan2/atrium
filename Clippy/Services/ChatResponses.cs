using System;
using System.Collections.Generic;
using System.Text;

namespace Clippy.Services;

public partial class ChatService
{

    public static string CommandString => string.Join("\n", ChatCommands.Registry.Select(c => c.Function + c.Parameters + " - " + c.Description));

    private const string PingMessage = "Please respond quickly and succinctly, you are learning tool with access to many functions. Please respond with the word Supercalifragilisticexpialidocious inside JSON format { \"response\" : \"...\" }. Only respond with the JSON and the word no other explanations needed. ";


    public static async Task<string?> StandardResponse(HttpClient Http, string clientId, string message)
    {

        Dictionary<DateTime, Tuple<bool, string>>? _recents = null;
        if (AllRecents?.TryGetValue(clientId, out _recents) != true)
        {
            _ = (AllRecents?[clientId] = _recents = []);
        }


        if (_recents?.LastOrDefault().Key != null && _recents?.Last().Key + TimeSpan.FromSeconds(10) > DateTime.Now)
        {
            _recents?.Add(DateTime.Now + TimeSpan.FromSeconds(1), new Tuple<bool, string>(true, "You're sending messages too quickly."));
            return "You're sending messages too quickly.";
        }


        var previous = JsonSerializer.Serialize(_recents?.TakeLast(10).Select(r => new RecentModel()
        {
            Role = r.Value.Item1 ? "assistant" : "user",
            Date = r.Key,
            Content = r.Value.Item2
        }));

        var result = await ExecutePost(Http, clientId, "The user writes:\n" + message
            + "\n\nIf it's directly related to a command, respond with JSON only like "
            + "{\"Function\": \"...\", \"Param1\" : \"...\"}. If you need to chain informational "
            + "commands together, use a list []:\n" + CommandString + "\n\nHistory for Context:\n"
            + previous);

        return result;
    }


}
