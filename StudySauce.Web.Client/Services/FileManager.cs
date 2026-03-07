using DataLayer.Entities;
using StudySauce.Shared.Services;
using System.Net;
using System.Net.Http.Json;

namespace StudySauce.Web.Client.Services
{
    public class FileManager : IFileManager
    {
        private readonly HttpClient _httpClient;
        internal int currentProgress = 0;
        public event Action<DataLayer.Entities.File?>? OnFileUploaded;
        public event Action<bool>? OnFileDragging;

        internal static IServiceProvider? _service;

        public FileManager()
        {
            _httpClient = _service.GetRequiredService<HttpClient>();
        }

        public async Task UploadFile(string localPath)
        {
            using var fileStream = System.IO.File.OpenRead(localPath);
            await UploadFile(fileStream, localPath);
        }

        public async Task UploadFile(Stream fileStream, string localPath)
        {
            var content = new MultipartFormDataContent();

            var streamContent = new ProgressableStreamContent(fileStream, 4096, (sent) =>
            {
                var percentage = (double)sent / fileStream.Length * 100;
                // Update your Blazor Progress Bar variable here
                currentProgress = (int)percentage;
            });

            content.Add(streamContent, "file", Path.GetFileName(localPath));

            var response = await _httpClient.PostAsync("api/upload", content);


        }

        public async Task OpenFileDialog()
        {

        }

        public async Task SetDragging(bool dragging)
        {
            OnFileDragging?.Invoke(dragging);
        }

        public async Task<Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<Card>>> InspectFile(string ankiPackage)
        {
            var response = await _httpClient.PostAsync("api/inspect?anki=" + ankiPackage, new StringContent("", System.Text.Encoding.UTF8, "application/json"));

            var result = await response.Content.ReadFromJsonAsync<Inspection>();
            return new Tuple<IEnumerable<DataLayer.Entities.File>, IEnumerable<Card>>(result.Files, result.Cards);
        }
    }


    public class Inspection
    {
        public List<DataLayer.Entities.File> Files { get; set; }
        public List<DataLayer.Entities.Card> Cards { get; set; }

    }

    public class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _fileStream;
        private readonly int _bufferSize;
        private readonly Action<long> _onProgress;

        public ProgressableStreamContent(Stream stream, int bufferSize, Action<long> onProgress)
        {
            _fileStream = stream;
            _bufferSize = bufferSize;
            _onProgress = onProgress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            long uploaded = 0;

            while (true)
            {
                var length = await _fileStream.ReadAsync(buffer, 0, buffer.Length);
                if (length <= 0) break;

                uploaded += length;
                await stream.WriteAsync(buffer, 0, length);
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
