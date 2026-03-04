namespace StudySauce.Shared.Services
{
    public interface IFileManager
    {
        Task UploadFile(string localPath);
        Task OpenFileDialog();
        event Action<DataLayer.Entities.File?>? OnFileUploaded;

    }
}
