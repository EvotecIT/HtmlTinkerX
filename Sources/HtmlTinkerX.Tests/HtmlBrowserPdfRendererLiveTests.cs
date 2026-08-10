using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task UrlCaptureUsesExplicitPrivateHostPolicyAndRequestHeaders() {
        await using LoopbackHtmlServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Correlation-Id"] = "capture-2048" });

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "URL invoice capture-2048");
        Assert.StartsWith(server.Url, result.Diagnostics.FinalUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HtmlCredentialsAreScopedToTheDeclaredOrigin() {
        await using LoopbackContentServer foreignOrigin = new(
            "<html><body><p id='foreign'>foreign-pending</p><script>document.querySelector('#foreign').textContent = localStorage.getItem('token') || 'cross-origin-clean';</script></body></html>");
        await using LoopbackContentServer declaredOrigin = new("same-origin-resource");
        string html = $"<html><body><p id='main'>main-pending</p><img src='/probe'><iframe style='width:600px;height:100px' src='{foreignOrigin.Url}'></iframe><script>document.querySelector('#main').textContent = localStorage.getItem('token') || 'main-missing';</script></body></html>";
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml(html, new Uri(declaredOrigin.Url)),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 750),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "origin-header" },
            localStorage: new System.Collections.Generic.Dictionary<string, string> { ["token"] = "origin-storage" });

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "origin-storage");
        AssertPdfContains(result.PdfBytes, "cross-origin-clean");
        Assert.Equal("origin-header", declaredOrigin.LastRenderToken);
        Assert.Null(foreignOrigin.LastRenderToken);
    }

    [Fact]
    public async Task HtmlBaseInjectionPreservesStandardsModeWithoutAnExplicitHead() {
        await using LoopbackContentServer origin = new("unused");
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<!doctype html><html><body><p id='mode'>pending</p></body></html>", new Uri(origin.Url)),
            beforeCaptureScript: "document.querySelector('#mode').textContent = document.compatMode;");

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "CSS1Compat");
    }

    [Fact]
    public async Task BlockedRequestSamplesNeverExceedTheConfiguredLimit() {
        string resources = string.Join(string.Empty, Enumerable.Range(0, 24).Select(index => $"<img src='http://127.0.0.1:{20000 + index}/blocked'>"));
        HtmlBrowserNetworkPolicy policy = new(blockedRequestDiagnosticLimit: 2);
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>bounded samples</p>" + resources + "</body></html>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 500));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        Assert.True(result.Diagnostics.BlockedRequestCount > 2);
        Assert.Equal(2, result.Diagnostics.BlockedRequests.Count);
    }

    [Fact]
    public async Task HtmlCaptureProducesReadablePdfAndReusesWarmBrowserWithIsolatedContexts() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            minimumBrowserInstances: 1,
            maximumBrowserInstances: 1));
        await renderer.PreWarmAsync();

        HtmlBrowserPdfResult first = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><h1>First isolated render</h1></body></html>"),
            beforeCaptureScript: "window.__htmlTinkerXLeak = 'should-not-survive';"));
        HtmlBrowserPdfResult second = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><h1>Second isolated render</h1><p id='state'></p></body></html>"),
            beforeCaptureScript: "document.querySelector('#state').textContent = window.__htmlTinkerXLeak || 'clean-context';"));

        AssertPdfContains(first.PdfBytes, "First isolated render");
        AssertPdfContains(second.PdfBytes, "Second isolated render");
        AssertPdfContains(second.PdfBytes, "clean-context");
        Assert.Equal(first.Diagnostics.BrowserInstanceId, second.Diagnostics.BrowserInstanceId);
        Assert.False(first.Diagnostics.BrowserReused);
        Assert.True(second.Diagnostics.BrowserReused);
        Assert.Equal(2, renderer.GetMetricsSnapshot().SucceededCaptures);
    }

    [Fact]
    public async Task FileCaptureResolvesSiblingResourcesWithinSelectedDirectory() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-PdfFile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string htmlPath = Path.Combine(root, "invoice.html");
        string cssPath = Path.Combine(root, "invoice.css");
        File.WriteAllText(cssPath, "h1 { color: rgb(17, 34, 51); }");
        File.WriteAllText(htmlPath, "<html><head><link rel='stylesheet' href='invoice.css'></head><body><h1>File invoice 1042</h1></body></html>");
        try {
            await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));
            HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromFile(htmlPath)));

            AssertPdfContains(result.PdfBytes, "File invoice 1042");
            Assert.Equal(0, result.Diagnostics.BlockedRequestCount);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ActiveReadinessCancellationAbortsPageAndLeavesRendererUsable() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(250));
        HtmlBrowserPdfRequest waiting = new(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>waiting</p></body></html>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, selector: "#never", timeout: 30000));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.CaptureAsync(waiting, cancellation.Token));

        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>recovered after cancellation</p></body></html>")));
        AssertPdfContains(recovered.PdfBytes, "recovered after cancellation");
        Assert.Equal(1, renderer.GetMetricsSnapshot().CancelledCaptures);
    }

    [Fact]
    public async Task ZeroLengthQueueRejectsExcessCaptureWithoutUnboundedWaiting() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            maximumQueuedCaptures: 0));
        HtmlBrowserPdfRequest slow = new(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>slow</p></body></html>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 750));
        Task<HtmlBrowserPdfResult> active = renderer.CaptureAsync(slow);
        await WaitUntilAsync(() => renderer.GetMetricsSnapshot().ActiveCaptures == 1, TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<HtmlBrowserPdfCapacityException>(() => renderer.CaptureAsync(slow));
        await active;
        Assert.Equal(1, renderer.GetMetricsSnapshot().RejectedCaptures);
    }

    [Fact]
    public async Task BrowserIsRecycledAfterConfiguredRenderCount() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            maximumRendersPerBrowser: 1));

        HtmlBrowserPdfResult first = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromHtml("<p>first</p>")));
        HtmlBrowserPdfResult second = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromHtml("<p>second</p>")));

        Assert.NotEqual(first.Diagnostics.BrowserInstanceId, second.Diagnostics.BrowserInstanceId);
        Assert.Equal(2, renderer.GetMetricsSnapshot().BrowsersRecycled);
    }

    [Fact]
    public async Task WebSocketRequestsUseTheSamePrivateNetworkPolicy() {
        await using LoopbackWebSocketServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));
        string html = $"<html><body data-ws='waiting'><p id='status'>websocket waiting</p><script>const done=s=>{{document.body.dataset.ws=s;document.querySelector('#status').textContent='websocket '+s;}}; const ws=new WebSocket('{server.Url}'); ws.onopen=()=>done('opened'); ws.onerror=()=>done('blocked'); ws.onclose=()=>done('blocked');</script></body></html>";
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml(html),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, function: "() => document.body.dataset.ws !== 'waiting'", timeout: 10000));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "websocket blocked");
        Assert.True(result.Diagnostics.BlockedRequestCount >= 1);
        Assert.Contains(result.Diagnostics.BlockedRequests, value => value.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllowedWebSocketUpgradeIsRelayedByThePolicyProxy() {
        await using LoopbackWebSocketServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        string html = $"<html><body data-ws='waiting'><p id='status'>websocket waiting</p><script>const done=s=>{{document.body.dataset.ws=s;document.querySelector('#status').textContent='websocket '+s;}}; const ws=new WebSocket('{server.Url}'); ws.onopen=()=>done('opened'); ws.onerror=()=>done('failed');</script></body></html>";
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml(html),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, function: "() => document.body.dataset.ws !== 'waiting'", timeout: 10000));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "websocket opened");
        Assert.Equal(0, result.Diagnostics.BlockedRequestCount);
    }

    [Fact]
    public async Task DisposeCancelsPendingCallerScriptAndDrainsTheLease() {
        HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1, maximumQueuedCaptures: 1));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<html><body>pending script</body></html>"),
            beforeCaptureScript: "new Promise(() => {})");
        Task<HtmlBrowserPdfResult> capture = renderer.CaptureAsync(request);
        await WaitUntilAsync(() => renderer.GetMetricsSnapshot().ActiveCaptures == 1, TimeSpan.FromSeconds(10));
        Task<HtmlBrowserPdfResult> queued = renderer.CaptureAsync(new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromHtml("<p>queued</p>")));
        await WaitUntilAsync(() => renderer.GetMetricsSnapshot().QueuedCaptures == 1, TimeSpan.FromSeconds(10));

        Task dispose = renderer.DisposeAsync().AsTask();
        Assert.Same(dispose, await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(10))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        await dispose;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => renderer.CaptureAsync(request));
    }

    [Fact]
    public async Task DisposeIsSafeWhenPreWarmIsStarting() {
        HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            minimumBrowserInstances: 1,
            maximumBrowserInstances: 1));

        Task preWarm = renderer.PreWarmAsync();
        Task dispose = renderer.DisposeAsync().AsTask();

        Assert.Same(dispose, await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(10))));
        try { await preWarm; } catch (OperationCanceledException) { }
        await dispose;
    }

    [Fact]
    public async Task RedirectCannotEscapeTheAllowedHostPolicy() {
        await using LoopbackRedirectServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "localhost" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl(server.Url)));

        Assert.Equal(0, server.PrivateRequests);
        Assert.True(result.Diagnostics.BlockedRequestCount >= 1);
        Assert.Contains(result.Diagnostics.BlockedRequests, value => value.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OriginScopedHeadersAreRemovedFromCrossOriginRedirects() {
        await using LoopbackRedirectServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "localhost", "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Secret"] = "must-not-leak" }));

        AssertPdfContains(result.PdfBytes, "private");
        Assert.Equal(1, server.PrivateRequests);
        Assert.Null(server.PrivateRenderSecret);
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task HttpsCertificateErrorsRequireAnExplicitOptIn() {
        await using LoopbackHttpsServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using (HtmlBrowserPdfRenderer strict = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1, networkPolicy: policy))) {
            await Assert.ThrowsAsync<PlaywrightException>(() => strict.CaptureAsync(
                new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl(server.Url))));
        }

        await using HtmlBrowserPdfRenderer trusted = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            ignoreHttpsErrors: true,
            networkPolicy: policy));
        HtmlBrowserPdfResult result = await trusted.CaptureAsync(
            new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl(server.Url)));

        AssertPdfContains(result.PdfBytes, "trusted TLS page");
    }
#endif

    private static void AssertPdfContains(byte[] bytes, string expectedText) {
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        using MemoryStream stream = new(bytes, writable: false);
        using PdfDocument document = PdfDocument.Open(stream);
        string text = string.Join(" ", document.GetPages().Select(page => page.Text));
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!predicate()) {
            if (stopwatch.Elapsed >= timeout) throw new TimeoutException("Condition was not reached before timeout.");
            await Task.Delay(25);
        }
    }

    private sealed class LoopbackHtmlServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;

        internal LoopbackHtmlServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/invoice";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string correlation = ReadHeader(request, "X-Correlation-Id") ?? "missing";
                    string body = $"<html><body><h1>URL invoice {System.Net.WebUtility.HtmlEncode(correlation)}</h1></body></html>";
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    string headers = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                    return;
                } catch (SocketException) when (_cancellation.IsCancellationRequested) {
                    return;
                }
            }
        }

        internal static string? ReadHeader(string request, string name) {
            foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None)) {
                int separator = line.IndexOf(':');
                if (separator > 0 && string.Equals(line.Substring(0, separator), name, StringComparison.OrdinalIgnoreCase)) {
                    return line.Substring(separator + 1).Trim();
                }
            }
            return null;
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            _cancellation.Dispose();
        }
    }

    private sealed class LoopbackContentServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private readonly string _body;
        private string? _lastRenderToken;

        internal LoopbackContentServer(string body) {
            _body = body;
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/origin";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastRenderToken => Volatile.Read(ref _lastRenderToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string? token = LoopbackHtmlServer.ReadHeader(request, "X-Render-Token");
                    if (token != null) Volatile.Write(ref _lastRenderToken, token);
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(_body);
                    byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, 0, headers.Length);
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

    private sealed class LoopbackWebSocketServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;

        internal LoopbackWebSocketServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"ws://127.0.0.1:{port}/private";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }

        private async Task ServeAsync() {
            try {
                using TcpClient client = await _listener.AcceptTcpClientAsync();
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[8192];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                string request = Encoding.ASCII.GetString(buffer, 0, read);
                string key = LoopbackHtmlServer.ReadHeader(request, "Sec-WebSocket-Key") ?? string.Empty;
                using SHA1 sha1 = SHA1.Create();
                string accept = Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n");
                await stream.WriteAsync(response, 0, response.Length);
                await Task.Delay(Timeout.Infinite, _cancellation.Token);
            } catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) {
            } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
            } catch (SocketException) when (_cancellation.IsCancellationRequested) {
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            _cancellation.Dispose();
        }
    }

    private sealed class LoopbackRedirectServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private int _privateRequests;
        private string? _privateRenderSecret;

        internal LoopbackRedirectServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://localhost:{port}/start";
            RedirectTarget = $"http://127.0.0.1:{port}/private";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        private string RedirectTarget { get; }
        internal int PrivateRequests => Volatile.Read(ref _privateRequests);
        internal string? PrivateRenderSecret => Volatile.Read(ref _privateRenderSecret);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[4096];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    byte[] response;
                    if (request.StartsWith("GET /private", StringComparison.Ordinal)) {
                        Interlocked.Increment(ref _privateRequests);
                        Volatile.Write(ref _privateRenderSecret, LoopbackHtmlServer.ReadHeader(request, "X-Render-Secret"));
                        response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 7\r\nConnection: close\r\n\r\nprivate");
                    } else {
                        response = Encoding.ASCII.GetBytes($"HTTP/1.1 302 Found\r\nLocation: {RedirectTarget}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    }
                    await stream.WriteAsync(response, 0, response.Length);
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

#if !NETFRAMEWORK
    private sealed class LoopbackHttpsServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly RSA _key = RSA.Create(2048);
        private readonly X509Certificate2 _certificate;
        private readonly Task _serverTask;

        internal LoopbackHttpsServer() {
            CertificateRequest request = new("CN=localhost", _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder names = new();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"https://127.0.0.1:{port}/certificate";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using SslStream stream = new(client.GetStream(), leaveInnerStreamOpen: false);
                    await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                        ServerCertificate = _certificate,
                        EnabledSslProtocols = SslProtocols.Tls12
                    }, _cancellation.Token);
                    byte[] request = new byte[4096];
                    int read = await stream.ReadAsync(request, 0, request.Length, _cancellation.Token);
                    if (read == 0) continue;
                    string body = "<html><body><p>trusted TLS page</p></body></html>";
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, 0, headers.Length, _cancellation.Token);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, _cancellation.Token);
                } catch (Exception ex) when (_cancellation.IsCancellationRequested && (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException || ex is IOException)) {
                    return;
                } catch (AuthenticationException) {
                    // The strict browser intentionally rejects this development certificate.
                } catch (IOException) {
                    // The strict browser can close immediately after certificate validation.
                }
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            _certificate.Dispose();
            _key.Dispose();
            _cancellation.Dispose();
        }
    }
#endif
}
