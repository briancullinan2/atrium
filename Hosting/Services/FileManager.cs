
using Microsoft.AspNetCore.Authorization;


namespace Hosting.Services;



public partial class FileManager(
    IQueryManager Query, ICircuitProvider Circuit
#if BROWSER
    , HttpClient Http
#endif
) : IFileManager
{
    public event Action<object?>? OnFileUploaded;
    public event Action<bool>? OnFileDragging;

    public static string UploadDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Uploads");

    static FileManager()
    {

        if (!Directory.Exists(UploadDirectory))
        {
            _ = Directory.CreateDirectory(UploadDirectory);
        }
    }


    public async Task<object?> UploadFile(string localPath)
    {
        using var localStream = System.IO.File.OpenRead(localPath);
        return await UploadFile(localStream, localPath);
    }



    public async Task<object?> UploadFile(Stream localStream, string localPath, string? source = "Uploads")
    {
        if(OperatingSystem.IsBrowser() && !Circuit.IsSignalCircuit)
        {
            return Circuit.InvokeAsync<object?>(ReceiveFileMethod.Route(), [localStream, localPath, source]);
        }
        return await ReceiveFile(localStream, localPath, source);
    }




    public async Task SetDragging(bool dragging)
    {
        OnFileDragging?.Invoke(dragging);
    }


    public static MethodInfo ReceiveFileMethod { get; } = typeof(FileManager).GetMethod(nameof(ReceiveFile)) ?? throw new InvalidOperationException("ReceiveFile method not found.");


    // TODO: generalize not just for anki and add a parameter like string source = "Uploads"
    //   so any implementer can designate themselves as the source of the data
    [AllowAnonymous]
    public async Task<object?> ReceiveFile(Stream? localStream, string localPath, string? source = "Uploads")
    {
        if(localStream == null)
            throw new InvalidOperationException("No files provided.");

        var savePath = Path.Combine(UploadDirectory, Path.GetFileName(localPath).ToSafe());
        using var fileStream = System.IO.File.Create(savePath);
        await localStream.CopyToAsync(fileStream);
        // TODO: store in database and return File entity?
        fileStream.Close();
        localStream.Close();

        var task = Query.Save(new DataShared.ForeignEntity.File()
        {
            Filename = savePath,
            Source = source // TODO: fill in from nav or parameter or something
        });


        if (task != null)
        {
            await task;
            var file = task.Result;
            OnFileUploaded?.Invoke(file);
            return file;
        }
        return null;
    }

}
