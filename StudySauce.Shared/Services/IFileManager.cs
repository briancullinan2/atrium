namespace StudySauce.Shared.Services
{
    public interface IFileManager
    {
        Task UploadFile(string localPath);
        Task UploadFile(Stream localFile, string localPath);
        Task<Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<DataLayer.Entities.Card>>> InspectFile(string ankiPackage);
        Task OpenFileDialog();
        Task SetDragging(bool dragging);
        event Action<DataLayer.Entities.File?>? OnFileUploaded;
        event Action<bool>? OnFileDragging;
    }
}
