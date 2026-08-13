using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task NestedDedicatedWorkerRequestsReceiveOriginScopedHeaders() {
        await using LoopbackWorkerHeaderServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'worker authorized'",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "worker-token" }));

        AssertPdfContains(result.PdfBytes, "worker authorized");
        Assert.Equal("worker-token", server.LastProtectedToken);
    }

    private sealed class LoopbackWorkerHeaderServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private string? _lastProtectedToken;

        internal LoopbackWorkerHeaderServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastProtectedToken => Volatile.Read(ref _lastProtectedToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string requestTarget = request.Split(' ')[1];
                    string contentType;
                    string body;
                    if (requestTarget.StartsWith("/worker.js", StringComparison.Ordinal)) {
                        contentType = "application/javascript; charset=utf-8";
                        body = $"const nestedSource = `fetch('{Url}protected').then(response => response.text()).then(text => postMessage(text));`; const nested = new Worker(URL.createObjectURL(new Blob([nestedSource], {{ type: 'application/javascript' }}))); nested.onmessage = event => postMessage(event.data);";
                    } else if (requestTarget.StartsWith("/protected", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = LastProtectedToken == "worker-token" ? "worker authorized" : "worker denied";
                    } else {
                        contentType = "text/html; charset=utf-8";
                        body = "<html><body><p id='result'>pending</p><script>const worker = new Worker('/worker.js'); worker.onmessage = event => document.querySelector('#result').textContent = event.data;</script></body></html>";
                    }
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                    return;
                } catch (SocketException) when (_cancellation.IsCancellationRequested) {
                    return;
                }
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            _cancellation.Dispose();
        }
    }
}
