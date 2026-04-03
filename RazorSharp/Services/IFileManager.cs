namespace FlashCard.Services
{
    public interface IFileManager
    {
        Task<DataLayer.Entities.File?> UploadFile(string localPath);
        Task<DataLayer.Entities.File?> UploadFile(Stream localFile, string localPath, string? source = "Uploads");
        Task OpenFileDialog();
        Task SetDragging(bool dragging);
        event Action<DataLayer.Entities.File?>? OnFileUploaded;
        event Action<bool>? OnFileDragging;
        Task<string?> OpenFile(string file);
    }
}
