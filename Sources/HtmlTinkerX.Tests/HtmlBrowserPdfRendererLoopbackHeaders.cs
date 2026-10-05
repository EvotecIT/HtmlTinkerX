using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    private static async Task<string?> ReadFixtureRequestHeadersAsync(NetworkStream stream, CancellationToken cancellationToken) {
        using CancellationTokenRegistration registration = cancellationToken.Register(stream.Dispose);
        byte[] buffer = new byte[1024];
        StringBuilder request = new();
        while (true) {
            int read;
            try {
                read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            } catch (Exception error) when (cancellationToken.IsCancellationRequested &&
                (error is IOException || error is ObjectDisposedException || error is OperationCanceledException)) {
                return null;
            }
            if (read == 0) return null;
            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (request.Length > 65536) throw new InvalidDataException("The loopback request headers exceed 64 KiB.");
            string headers = request.ToString();
            if (headers.IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0) return headers;
        }
    }
}
