namespace FlashCard.Services
{
    public interface IFileManager
    {
        Task UploadFile(string localPath);
        Task UploadFile(Stream localFile, string localPath, string? source = "Uploads");
        Task OpenFileDialog();
        Task SetDragging(bool dragging);
        event Action<DataLayer.Entities.File?>? OnFileUploaded;
        event Action<bool>? OnFileDragging;
        Task<string?> OpenFile(string file);
    }
}
