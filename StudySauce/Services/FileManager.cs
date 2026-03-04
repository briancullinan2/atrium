using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using StudySauce.Shared.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudySauce.Services
{
    internal class FileManager : IFileManager
    {
        public event Action<DataLayer.Entities.File?>? OnFileUploaded;

        public async Task UploadFile(string localPath)
        {
            using var localStream = System.IO.File.OpenRead(localPath);
            var savePath = Path.Combine(AppContext.BaseDirectory, "Uploads", Path.GetFileName(localPath).ToSafe());
            using var fileStream = System.IO.File.Create(savePath);
            await localStream.CopyToAsync(fileStream);
            // TODO: store in database and return File entity?
        }

        //[HttpPost("upload")]
        public static async Task OnUploadFile(HttpContext context, IServiceProvider _service)
        {
            try
            {
                // 1. Check if this is a file upload (Multipart)
                if (context.Request.HasFormContentType && context.Request.Form.Files.Any())
                {
                    foreach (var file in context.Request.Form.Files)
                    {
                        // Accessing the stream directly from the request
                        using var stream = file.OpenReadStream();

                        // Example: Save to disk in Arizona-based storage
                        var savePath = Path.Combine(AppContext.BaseDirectory, "Uploads", Path.GetFileName(file.FileName).ToSafe());
                        using var fileStream = System.IO.File.Create(savePath);
                        await stream.CopyToAsync(fileStream);

                        // Now you can log the file entry into your TranslationContext 
                        // using the 'file.FileName' or 'file.Length'
                    }

                }
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(ex.Message, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });
                await context.Response.WriteAsync(json);
            }
        }


        public async Task OpenFileDialog()
        {
            try
            {
                // This calls the native Windows picker, bypassing WebView2 bugs
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Anki Package",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                        { DevicePlatform.WinUI, (string[])[".apkg", ".zip"] }
                    }) // or custom types
                });

                if (result != null)
                {
                    // You get the ABSOLUTE path immediately! 
                    // No more "browser sandbox" stream restrictions.
                    var fullPath = result.FullPath;
                    //await HandleFile(fullPath);
                }
            }
            catch (Exception ex)
            {
                // Handle cancel or permission issues
            }
        }

    }
}
