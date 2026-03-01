using System.Net;

namespace Personal.Dashboard.Test.Support.Common.Http;

public class InMemoryHttpContent(byte[] buffer) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(buffer, 0, buffer.Length);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = buffer.Length;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        
    }
}