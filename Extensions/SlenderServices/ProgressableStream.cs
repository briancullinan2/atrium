using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Extensions.SlenderServices;



public partial class ProgressableStreamContent(Stream stream, int bufferSize, Action<long> onProgress) : HttpContent
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
