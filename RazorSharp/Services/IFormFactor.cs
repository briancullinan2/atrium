namespace RazorSharp.Services
{
    public interface IFormFactor
    {
        string GetFormFactor();
        string GetPlatform();
        string BaseUrl { get; }
        Task StopAsync();

    }
}
