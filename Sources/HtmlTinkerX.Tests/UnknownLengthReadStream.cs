using System.IO;

namespace HtmlTinkerX.Tests;

// StreamContent derives Content-Length from seekable streams.
internal sealed class UnknownLengthReadStream(byte[] bytes) : MemoryStream(bytes, writable: false) {
    public override bool CanSeek => false;
}
