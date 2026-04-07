using System;
using System.Collections.Generic;
using System.Text;

namespace Clippy.Services;

public partial class ChatService
{


    private static readonly string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string savedSettings = Path.Combine(homeDirectory, ".credentials", "atrium-chat.json");

    protected static readonly List<ServicePreset> Settings;

    static ChatService()
    {
        if (System.IO.File.Exists(savedSettings))
        {
            try
            {
                Settings = JsonSerializer.Deserialize<List<ServicePreset>>(System.IO.File.ReadAllText(savedSettings));
            }
            catch (Exception) { }
        }
        Settings ??= [];
    }



    protected static void SaveWorkingSettings(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters)
    {
        if (string.IsNullOrWhiteSpace(ServiceUrl)) return;

        // TODO: save service information
        var index = Settings.FindIndex(s => string.Equals(s.Url, ServiceUrl, StringComparison.InvariantCultureIgnoreCase));
        var replacementPreset = new ServicePreset()
        {
            ApiKey = ApiKey,
            Url = ServiceUrl,
            DefaultModel = ModelName,
            ResponsePath = Response,
            Parameters = Parameters,
            IsPrevious = true,
        };

        if (Settings.Count == 0 || Settings.FirstOrDefault(s => s.IsDefault) == null)
        {
            replacementPreset.IsDefault = true;
        }

        if (index > -1)
        {
            Settings[index] = replacementPreset;
        }
        else
        {
            Settings.Add(replacementPreset);
        }

        var validSettings = JsonSerializer.Serialize(Settings, JsonExtensions.Default);
        System.IO.File.WriteAllText(savedSettings, validSettings);
    }

}
