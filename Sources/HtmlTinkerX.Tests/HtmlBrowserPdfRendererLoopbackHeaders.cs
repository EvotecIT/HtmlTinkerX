using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task RedirectFixtureReadsHeadersSplitAcrossPacketsBeforeCheckingScopedSecrets() {
        await using LoopbackRedirectServer server = new();
        using TcpClient connection = new();
        int port = new Uri(server.Url).Port;
        await connection.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream stream = connection.GetStream();
        byte[] prefix = Encoding.ASCII.GetBytes($"GET /private HTTP/1.1\r\nHost: localhost:{port}\r\nX-Render-");
        await stream.WriteAsync(prefix, 0, prefix.Length);
        byte[] responseBuffer = new byte[256];
        Task<int> responseRead = stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
        Assert.NotSame(responseRead, await Task.WhenAny(responseRead, Task.Delay(250)));
        byte[] remainingHeaders = Encoding.ASCII.GetBytes("Secret: supplied\r\n\r\n");
        await stream.WriteAsync(remainingHeaders, 0, remainingHeaders.Length);
        Assert.Same(responseRead, await Task.WhenAny(responseRead, Task.Delay(5000)));
        int read = await responseRead;
        Assert.Contains("HTTP/1.1 200 OK", Encoding.ASCII.GetString(responseBuffer, 0, read));
        Assert.Equal("supplied", server.PrivateRenderSecret);
    }

    [Fact]
    public async Task ContentFixtureReadsHeadersSplitAcrossPacketsBeforeCheckingScopedTokens() {
        await using LoopbackContentServer server = new("verified");
        using TcpClient connection = new();
        int port = new Uri(server.Url).Port;
        await connection.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream stream = connection.GetStream();
        byte[] prefix = Encoding.ASCII.GetBytes($"GET /origin HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nX-Render-");
        await stream.WriteAsync(prefix, 0, prefix.Length);
        byte[] responseBuffer = new byte[256];
        Task<int> responseRead = stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
        Assert.NotSame(responseRead, await Task.WhenAny(responseRead, Task.Delay(250)));
        byte[] remainingHeaders = Encoding.ASCII.GetBytes("Token: supplied\r\n\r\n");
        await stream.WriteAsync(remainingHeaders, 0, remainingHeaders.Length);
        Assert.Same(responseRead, await Task.WhenAny(responseRead, Task.Delay(5000)));
        Assert.True(await responseRead > 0);
        Assert.Equal("supplied", server.LastRenderToken);
    }

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
