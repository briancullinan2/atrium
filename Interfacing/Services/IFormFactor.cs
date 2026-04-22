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
    Type? RequestControl { get; set; }
    List <IFile> Files { get; }
    //Task SetState();

    ValueTask Clipboard(string text);

    Dictionary<string, string>? QueryParameters { get; }
    Task SetSessionCookie(string name, string value, int days);
    Task<string?> GetSessionCookie(string name);
}


public interface ITitleService
{
    event Action<string?>? OnTitleChanged;
    Task<string?> UpdateTitle(string? title);
    Task<string?> SetDefaultTitle(Type? controlType);
}

public interface IHasName
{
    static abstract string? AppName { get; }
}


public interface IWindowManager : IHasInstall, ITitleService
{
    Task ExpandWindow(bool expanding);
    Task<nint> GetWindowHwnd();
    Task CreateTrayIcon();
    bool IsSplashMode { get; }
}


public interface IFile : IHasNoService
{
    string FileName { get; }
    string Name { get; }
    //DateTimeOffset LastModified { get; }
    long Size { get; }
    string ContentType { get; }
    Stream OpenReadStream();
}

public interface IHasNoService
{

}
