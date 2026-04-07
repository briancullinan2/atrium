

namespace Extensions.SlenderServices;

public interface IFileManager
{
    Task<object?> UploadFile(string localPath);
    Task<object?> UploadFile(Stream localFile, string localPath, string? source = "Uploads");
    Task<object?> ReceiveFile(Stream localFile, string localPath, string? source = "Uploads");
    Task OpenFileDialog();
    Task SetDragging(bool dragging);
    event Action<object?>? OnFileUploaded;
    event Action<bool>? OnFileDragging;
    Task<string?> OpenFile(string file);
}
