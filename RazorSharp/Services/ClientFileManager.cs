using FlashCard.Services;
using System.Net;
using System.Net.Http.Json;

namespace WebClient.Services
{
    public class FileManager : IFileManager
    {
        private static HttpClient? _httpClient;
        internal int currentProgress = 0;
        public event Action<DataLayer.Entities.File?>? OnFileUploaded;
        public event Action<bool>? OnFileDragging;

        public FileManager(HttpClient client)
        {
            _httpClient ??= client;
        }

        public async Task<DataLayer.Entities.File?> UploadFile(string localPath)
        {
            using var fileStream = File.OpenRead(localPath);
            return await UploadFile(fileStream, localPath);
        }

        public async Task<DataLayer.Entities.File?> UploadFile(Stream fileStream, string localPath, string? source = "Uploads")
        {
            if (_httpClient == null)
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

            var response = await _httpClient.PostAsync("/api/upload", content);
            var result = await response.Content.ReadFromJsonAsync<DataLayer.Entities.File>();
            OnFileUploaded?.Invoke(result);
            return result;
            // TODO: update file list wasn't implemented until after saving

        }

        public async Task OpenFileDialog()
        {

        }

        public async Task SetDragging(bool dragging)
        {
            OnFileDragging?.Invoke(dragging);
        }

        public async Task<string?> OpenFile(string file)
        {
            var result = _httpClient?.GetStringAsync(file);
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
