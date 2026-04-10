namespace Interfacing.Services;

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
    List<IFile> Files { get; }

    event Action<string?>? OnTitleChanged;
    Dictionary<string, string>? QueryParameters { get; }
    Task SetSessionCookie(string name, string value, int days);
    Task<string?> GetSessionCookie(string name);
}


public interface ITitleService
{
    Task<string?> UpdateTitle(string? title);
    static abstract string? AppName { get; }
}


public interface IHasWindow
{
    Task ExpandWindow(bool expanding);
    bool IsSplashMode { get; }
}


public interface IFile
{
    string FileName { get; }
    string Name { get; }
    //DateTimeOffset LastModified { get; }
    long Size { get; }
    string ContentType { get; }
    Stream OpenReadStream();
}
