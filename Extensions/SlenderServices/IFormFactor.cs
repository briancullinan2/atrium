using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices
{
    public interface IFormFactor
    {
        string GetFormFactor();
        string GetPlatform();
        Task StopAsync();
        string BaseUrl { get; }
        bool IsBrowser { get; }
        bool IsWebContext { get; }
        bool IsMauiContext { get; }
        string ConnectionId { get; }
        Task<string?> UpdateTitle(string? title);
        event Action<string?>? OnTitleChanged;
    }


}
