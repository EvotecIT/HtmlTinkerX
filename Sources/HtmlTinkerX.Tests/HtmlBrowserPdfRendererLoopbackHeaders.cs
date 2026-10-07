using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedirectFixtureServesLocalhostOnBothAddressFamilies(bool ipv6) {
        if (ipv6 && !Socket.OSSupportsIPv6) return;
        await using LoopbackRedirectServer server = new();
        using TcpClient connection = new(ipv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
        int port = new Uri(server.Url).Port;
        await connection.ConnectAsync(ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, port);
        using NetworkStream stream = connection.GetStream();
        byte[] request = Encoding.ASCII.GetBytes($"GET /start HTTP/1.1\r\nHost: localhost:{port}\r\n\r\n");
        await stream.WriteAsync(request, 0, request.Length);
        byte[] response = new byte[512];
        Task<int> read = stream.ReadAsync(response, 0, response.Length);
        Assert.Same(read, await Task.WhenAny(read, Task.Delay(5000)));
        string headers = Encoding.ASCII.GetString(response, 0, await read);
        Assert.Contains("HTTP/1.1 302 Found", headers);
        Assert.Contains("Location: " + server.RedirectTarget, headers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedirectFixtureRespondsWhileAnotherConnectionHasIncompleteHeaders(bool partialHeaders) {
        await using LoopbackRedirectServer server = new();
        int port = new Uri(server.Url).Port;
        using TcpClient idle = new();
        await idle.ConnectAsync(IPAddress.Loopback, port);
        if (partialHeaders) {
            byte[] incomplete = Encoding.ASCII.GetBytes($"GET /private HTTP/1.1\r\nHost: localhost:{port}\r\n");
            await idle.GetStream().WriteAsync(incomplete, 0, incomplete.Length);
        }

        using TcpClient connection = new();
        await connection.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream stream = connection.GetStream();
        byte[] request = Encoding.ASCII.GetBytes($"GET /private HTTP/1.1\r\nHost: localhost:{port}\r\nX-Render-Secret: supplied\r\n\r\n");
        await stream.WriteAsync(request, 0, request.Length);
        byte[] responseBuffer = new byte[256];
        Task<int> responseRead = stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

        Assert.Same(responseRead, await Task.WhenAny(responseRead, Task.Delay(5000)));
        Assert.Contains("HTTP/1.1 200 OK", Encoding.ASCII.GetString(responseBuffer, 0, await responseRead));
        Assert.Equal("supplied", server.PrivateRenderSecret);
        Assert.Equal(1, server.PrivateRequests);
    }

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
    private sealed class LoopbackRedirectServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly TcpListener? _ipv6Listener;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentDictionary<Task, byte> _connections = new();
        private readonly Task _serverTask;
        private int _privateRequests;
        private string? _privateRenderSecret;

        internal LoopbackRedirectServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            if (Socket.OSSupportsIPv6) {
                _ipv6Listener = new TcpListener(IPAddress.IPv6Loopback, port);
                _ipv6Listener.Server.DualMode = false;
                _ipv6Listener.Start();
            }
            Url = $"http://localhost:{port}/start";
            RedirectTarget = $"http://127.0.0.1:{port}/private";
            _serverTask = _ipv6Listener == null
                ? ServeAsync(_listener)
                : Task.WhenAll(ServeAsync(_listener), ServeAsync(_ipv6Listener));
        }

        internal string Url { get; }
        internal string RedirectTarget { get; }
        internal int PrivateRequests => Volatile.Read(ref _privateRequests);
        internal string? PrivateRenderSecret => Volatile.Read(ref _privateRenderSecret);

        private async Task ServeAsync(TcpListener listener) {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    Task connection = HandleConnectionAsync(await listener.AcceptTcpClientAsync());
                    _connections[connection] = 0;
                    _ = connection.ContinueWith(completed => {
                        _connections.TryRemove(completed, out _);
                        _ = completed.Exception;
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                    return;
                } catch (SocketException) when (_cancellation.IsCancellationRequested) {
                    return;
                }
            }
        }

        private async Task HandleConnectionAsync(TcpClient client) {
            using (client)
            using (NetworkStream stream = client.GetStream()) {
                try {
                    string? request = await ReadFixtureRequestHeadersAsync(stream, _cancellation.Token);
                    if (request == null) return;
                    byte[] response;
                    if (request.StartsWith("GET /private", StringComparison.Ordinal)) {
                        Interlocked.Increment(ref _privateRequests);
                        Volatile.Write(ref _privateRenderSecret, LoopbackHtmlServer.ReadHeader(request, "X-Render-Secret"));
                        response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 7\r\nConnection: close\r\n\r\nprivate");
                    } else {
                        response = Encoding.ASCII.GetBytes($"HTTP/1.1 302 Found\r\nLocation: {RedirectTarget}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    }
                    await stream.WriteAsync(response, 0, response.Length, _cancellation.Token);
                } catch (IOException) {
                    // A disconnected client must not stop the remaining redirect checks.
                } catch (Exception error) when (_cancellation.IsCancellationRequested &&
                    (error is ObjectDisposedException || error is OperationCanceledException || error is SocketException)) {
                }
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            _ipv6Listener?.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            await Task.WhenAll(_connections.Keys);
            _cancellation.Dispose();
        }
    }

}
