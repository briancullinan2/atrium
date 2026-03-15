namespace FlashCard.Services
{
    public interface ILocalServer
    {
        string BaseUrl { get; }
        Task StopAsync();
    }

}
