

namespace Hosting.Services
{
   

    public partial class FileManager(IQueryManager Query, HttpClient Http) : IFileManager
    {
        public event Action<object?>? OnFileUploaded;
        public event Action<bool>? OnFileDragging;

        public static string UploadDirectory = Path.Combine(AppContext.BaseDirectory, "Uploads");

        static FileManager()
        {

            if (!Directory.Exists(UploadDirectory))
            {
                _ = Directory.CreateDirectory(UploadDirectory);
            }
        }



#if WINDOWS
        //[HttpPost("upload")]
        public static async Task OnUploadFile(HttpContext context, IFileManager FileManager)
        {
            try
            {
                if (!context.Request.HasFormContentType || !context.Request.Form.Files.Any())
                {
                    throw new InvalidOperationException("No files provided.");
                }

                var first = true;
                foreach (var file in context.Request.Form.Files)
                {
                    // Accessing the stream directly from the request
                    using var stream = file.OpenReadStream();

                    // Example: Save to disk in Arizona-based storage
                    var databaseFile = await FileManager.UploadFile(stream, file.FileName);

                    if (!first) continue;
                    first = false;

                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(databaseFile, JsonExtensions.Default);
                    await context.Response.WriteAsync(json);
                    await context.Response.Body.FlushAsync();
                    await context.Response.CompleteAsync();

                }
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                var json = JsonSerializer.Serialize(ex.Message, JsonExtensions.Default);
                await context.Response.WriteAsync(json);
            }
        }


#endif

        public async Task SetDragging(bool dragging)
        {
            OnFileDragging?.Invoke(dragging);
        }


#if WINDOWS
        
#endif



        internal int currentProgress = 0;



        public async Task<File?> UploadFile(string localPath)
        {
            using var localStream = System.IO.File.OpenRead(localPath);
            return await UploadFile(localStream, localPath);
        }


        // TODO: generalize not just for anki and add a parameter like string source = "Uploads"
        //   so any implementer can designate themselves as the source of the data

        public async Task<File?> ReceiveFile(Stream localStream, string localPath, string? source = "Uploads")
        {
            var savePath = Path.Combine(UploadDirectory, Path.GetFileName(localPath).ToSafe());
            using var fileStream = System.IO.File.Create(savePath);
            await localStream.CopyToAsync(fileStream);
            // TODO: store in database and return File entity?
            fileStream.Close();
            localStream.Close();

            var task = Query.Save(new File()
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

        public async Task<File?> UploadFile(Stream fileStream, string localPath, string? source = "Uploads")
        {
            if (Http == null)
            {
                throw new InvalidOperationException("No Http client.");
            }

            var content = new MultipartFormDataContent();

            var streamContent = new ProgressableStreamContent(fileStream, 4096, (sent) =>
            {
                var percentage = (double)sent / fileStream.Length * 100;
                // Update your Blazor Progress Bar variable here
                currentProgress = (int)percentage;
            });

            content.Add(streamContent, "file", Path.GetFileName(localPath));

            var response = await Http.PostAsync("/api/upload", content);
            var result = await response.Content.ReadFromJsonAsync<File>();
            OnFileUploaded?.Invoke(result);
            return result;
            // TODO: update file list wasn't implemented until after saving

        }

        public async Task<string?> OpenFile(string file)
        {
            var result = Http.GetStringAsync(file);
            if (result == null) return null;
            return await result;
        }
    }


    public class ProgressableStreamContent(Stream stream, int bufferSize, Action<long> onProgress) : HttpContent
    {
        private readonly Stream _fileStream = stream;
        private readonly int _bufferSize = bufferSize;
        private readonly Action<long> _onProgress = onProgress;

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            long uploaded = 0;

            while (true)
            {
                var length = await _fileStream.ReadAsync(buffer);
                if (length <= 0) break;

                uploaded += length;
                await stream.WriteAsync(buffer.AsMemory(0, length));
                _onProgress(uploaded); // Trigger progress update
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _fileStream.Length;
            return true;
        }


    }
}
