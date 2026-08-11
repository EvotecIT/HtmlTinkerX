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
    public async Task SameOriginPopupRequestsReceiveOriginScopedHeaders() {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 5000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "window.open('/header-popup', '_blank'); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Theory]
    [InlineData("noopener")]
    [InlineData("noreferrer")]
    public async Task OpenerSuppressingPopupsNavigateWithOriginScopedHeaders(string features) {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.NoOpenerHeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => window.popupReturnedNull && document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 5000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: $"window.popupReturnedNull = window.open('/header-popup-noopener', '_blank', '{features}') === null; true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
        if (features == "noreferrer") Assert.Null(server.LastPopupReferer);
    }

    [Fact]
    public async Task WebStorageDoesNotReseedAnIndependentPopup() {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.StorageUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'updated'",
                timeout: 5000),
            localStorage: new System.Collections.Generic.Dictionary<string, string> { ["token"] = "one-time" },
            beforeCaptureScript: "localStorage.setItem('token', 'updated'); window.open('/storage-popup', '_blank', 'noopener'); true"));

        AssertPdfContains(result.PdfBytes, "updated");
    }

    [Fact]
    public async Task DedicatedWorkerRequestsReceiveOriginScopedHeaders() {
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
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "worker-token" }));

        AssertPdfContains(result.PdfBytes, "worker authorized");
        Assert.Equal("worker-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task WebStorageSeedsOnlyTheInitialTopLevelDocument() {
        const string html = "<html><body><p id='result'>pending</p><script>document.querySelector('#result').textContent = localStorage.getItem('token') || 'not-restored';</script></body></html>";
        await using LoopbackContentServer server = new(html);
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'not-restored'",
                timeout: 5000),
            localStorage: new System.Collections.Generic.Dictionary<string, string> { ["token"] = "one-time" },
            beforeCaptureScript: "localStorage.removeItem('token'); setTimeout(() => location.reload(), 0); true"));

        AssertPdfContains(result.PdfBytes, "not-restored");
    }

    [Fact]
    public async Task HtmlStringWithVirtualFileBaseLoadsSiblingResourcesFromAFileOrigin() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-FileBase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "message.js"), "document.querySelector('#result').textContent = 'local resource loaded';");
        try {
            Uri baseUri = new(Path.Combine(Path.GetFullPath(root), "virtual-report.html"));
            const string html = "<html><body><p id='result'>pending</p><script src='message.js'></script></body></html>";
            await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));

            HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromHtml(html, baseUri),
                readiness: new HtmlBrowserPdfReadiness(
                    skipLoadState: true,
                    function: "() => document.querySelector('#result').textContent === 'local resource loaded'",
                    timeout: 5000)));

            AssertPdfContains(result.PdfBytes, "local resource loaded");
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PreCaptureScriptTimeoutReleasesTheBrowserLease() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<p>blocked script</p>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true),
            beforeCaptureScript: "new Promise(() => {})",
            beforeCaptureScriptTimeout: 100)));

        Assert.Contains("pre-capture script", exception.Message, StringComparison.OrdinalIgnoreCase);
        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<p>lease recovered</p>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true)));
        AssertPdfContains(recovered.PdfBytes, "lease recovered");
    }

    [Fact]
    public async Task PdfGenerationTimeoutRecyclesTheBrowserWithoutRetrying() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));
        string largeHtml = "<html><body>" + string.Concat(System.Linq.Enumerable.Repeat("<div>deadline content</div>", 10000)) + "</body></html>";

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(largeHtml),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true),
            pdfTimeout: 1)));

        Assert.Contains("PDF generation", exception.Message, StringComparison.OrdinalIgnoreCase);
        HtmlBrowserPdfRendererMetrics timedOut = renderer.GetMetricsSnapshot();
        Assert.Equal(0, timedOut.BrowserFailureRetries);
        Assert.True(timedOut.BrowsersRecycled >= 1);

        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<p>PDF lease recovered</p>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true)));
        AssertPdfContains(recovered.PdfBytes, "PDF lease recovered");
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
                        body = "fetch('/protected').then(response => response.text()).then(text => postMessage(text));";
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

    private sealed class LoopbackPopupServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private string? _lastPopupToken;
        private string? _lastProtectedToken;
        private string? _lastPopupReferer;

        internal LoopbackPopupServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            HeaderUrl = $"http://127.0.0.1:{port}/header-main";
            NoOpenerHeaderUrl = $"http://127.0.0.1:{port}/header-noopener-main";
            StorageUrl = $"http://127.0.0.1:{port}/storage-main";
            _serverTask = ServeAsync();
        }

        internal string HeaderUrl { get; }
        internal string NoOpenerHeaderUrl { get; }
        internal string StorageUrl { get; }
        internal string? LastPopupToken => Volatile.Read(ref _lastPopupToken);
        internal string? LastProtectedToken => Volatile.Read(ref _lastProtectedToken);
        internal string? LastPopupReferer => Volatile.Read(ref _lastPopupReferer);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string requestTarget = request.Split(' ')[1];
                    string contentType = "text/html; charset=utf-8";
                    string body;
                    if (requestTarget.StartsWith("/header-popup-noopener", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        Volatile.Write(ref _lastPopupReferer, LoopbackHtmlServer.ReadHeader(request, "Referer"));
                        body = "<script>fetch('/protected').then(response => response.text()).then(text => localStorage.setItem('popup-result', text));</script>";
                    } else if (requestTarget.StartsWith("/header-popup", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = "<script>fetch('/protected').then(response => response.text()).then(text => opener.postMessage(text, '*'));</script>";
                    } else if (requestTarget.StartsWith("/protected", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token" ? "popup authorized" : "popup denied";
                    } else if (requestTarget.StartsWith("/storage-popup", StringComparison.Ordinal)) {
                        body = "<script>localStorage.setItem('observed', localStorage.getItem('token') || 'missing'); close();</script>";
                    } else if (requestTarget.StartsWith("/storage-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => document.querySelector('#result').textContent = localStorage.getItem('observed') || 'pending', 20);</script>";
                    } else if (requestTarget.StartsWith("/header-noopener-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => document.querySelector('#result').textContent = localStorage.getItem('popup-result') || 'pending', 20);</script>";
                    } else {
                        body = "<p id='result'>pending</p><script>addEventListener('message', event => document.querySelector('#result').textContent = event.data);</script>";
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
