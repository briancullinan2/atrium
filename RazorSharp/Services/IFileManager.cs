namespace RazorSharp.Services
{
    public interface IFileManager
    {
        Task<File?> UploadFile(string localPath);
        Task<File?> UploadFile(Stream localFile, string localPath, string? source = "Uploads");
        Task OpenFileDialog();
        Task SetDragging(bool dragging);
        event Action<File?>? OnFileUploaded;
        event Action<bool>? OnFileDragging;
        Task<string?> OpenFile(string file);
    }
}
