using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task VisualMaskUsesAnOpaqueOverlayForReplacedContentAndRestoresThePage() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<img id='secret' alt='sensitive image' data-htmltinkerx-visual-mask='page-owned' style='width:120px;height:80px' src='data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw=='>");

        string masked = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            session.Page,
            maskSensitiveElements: false,
            maskSelectors: new[] { "#secret" },
            maskColor: "#00ff00",
            action: () => session.Page.EvaluateAsync<string>(@"() => {
                const secret = document.querySelector('#secret');
                const overlay = document.querySelector('[data-htmltinkerx-visual-mask-overlay]');
                const rect = secret.getBoundingClientRect();
                const overlayRect = overlay.getBoundingClientRect();
                return [
                    getComputedStyle(secret).visibility,
                    document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').length,
                    getComputedStyle(overlay).backgroundColor,
                    getComputedStyle(overlay).backgroundImage,
                    overlayRect.width >= rect.width && overlayRect.height >= rect.height
                ].join('|');
            }"),
            cancellationToken: CancellationToken.None);

        Assert.StartsWith("hidden|1|rgb(0, 0, 0)|", masked, StringComparison.Ordinal);
        Assert.Contains("rgb(0, 255, 0)", masked, StringComparison.Ordinal);
        Assert.EndsWith("|true", masked, StringComparison.Ordinal);
        Assert.Equal(
            "visible|0|width:120px;height:80px|page-owned",
            await session.Page.EvaluateAsync<string>("() => { const secret = document.querySelector('#secret'); return getComputedStyle(secret).visibility + '|' + document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').length + '|' + secret.getAttribute('style') + '|' + secret.getAttribute('data-htmltinkerx-visual-mask'); }"));
    }

    [Fact]
    public async Task VisualMaskCoversSvgElementsAndRestoresTheirInlineStyle() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<svg width='160' height='90'><rect id='secret' width='120' height='70' fill='red' style='opacity:0.75'></rect></svg>");

        string masked = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            session.Page,
            maskSensitiveElements: false,
            maskSelectors: new[] { "#secret" },
            maskColor: "#000000",
            action: () => session.Page.EvaluateAsync<string>(@"() => {
                const secret = document.querySelector('#secret');
                const overlay = document.querySelector('[data-htmltinkerx-visual-mask-overlay]');
                return getComputedStyle(secret).visibility + '|' + (overlay !== null) + '|' + overlay.getBoundingClientRect().width;
            }"),
            cancellationToken: CancellationToken.None);

        Assert.Equal("hidden|true|120", masked);
        Assert.Equal(
            "visible|opacity:0.75|0",
            await session.Page.EvaluateAsync<string>("() => { const secret = document.querySelector('#secret'); return getComputedStyle(secret).visibility + '|' + secret.getAttribute('style') + '|' + document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').length; }"));
    }

    [Fact]
    public async Task InvalidVisualMaskSelectorFailsClosedBeforeChangingThePage() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<p id='secret' style='color:red'>sensitive</p>");

        await Assert.ThrowsAsync<PlaywrightException>(() => HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            session.Page,
            maskSensitiveElements: false,
            maskSelectors: new[] { "#secret", ":not(" },
            maskColor: "#000000",
            action: () => Task.FromResult(true),
            cancellationToken: CancellationToken.None));

        Assert.Equal(
            "visible|color:red|0",
            await session.Page.EvaluateAsync<string>("() => { const secret = document.querySelector('#secret'); return getComputedStyle(secret).visibility + '|' + secret.getAttribute('style') + '|' + document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').length; }"));
    }

    [Fact]
    public async Task VisualMaskTraversesNestedOpenShadowRoots() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<div id='outer'></div>");
        await session.Page.EvaluateAsync(@"() => {
            const outer = document.querySelector('#outer').attachShadow({ mode: 'open' });
            const innerHost = document.createElement('div');
            outer.appendChild(innerHost);
            const inner = innerHost.attachShadow({ mode: 'open' });
            inner.innerHTML = `<input id='secret' type='password' style='width:140px;height:40px'>`;
        }");

        string masked = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            session.Page,
            maskSensitiveElements: false,
            maskSelectors: new[] { "#secret" },
            maskColor: "#000000",
            action: () => session.Page.EvaluateAsync<string>(@"() => {
                const secret = document.querySelector('#outer').shadowRoot.querySelector('div').shadowRoot.querySelector('#secret');
                return getComputedStyle(secret).visibility + '|' + document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').length;
            }"),
            cancellationToken: CancellationToken.None);

        Assert.Equal("hidden|1", masked);
        Assert.Equal(
            "visible|width:140px;height:40px|0",
            await session.Page.EvaluateAsync<string>("() => { const secret = document.querySelector('#outer').shadowRoot.querySelector('div').shadowRoot.querySelector('#secret'); return getComputedStyle(secret).visibility + '|' + secret.getAttribute('style') + '|' + document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').length; }"));
    }

    [Fact]
    public async Task NearFuturePersistentCookieRemainsAvailableDuringCapture() {
        await using LoopbackHtmlServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            cookies: new[] {
                new HtmlBrowserPdfCookie(
                    "render-session",
                    "short-lived",
                    url: server.Url,
                    expires: DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds())
            }));

        AssertPdfContains(result.PdfBytes, "render-session=short-lived");
    }

    [Fact]
    public async Task CaptureStyleSheetAppliesToAttachedChildFrames() {
        const string html = "<p id='result'>pending</p><iframe srcdoc=\"<p id='framed'>framed</p>\"></iframe>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            styleSheetContent: "#framed { display: none; }",
            beforeCaptureScript: "document.querySelector('#result').textContent = getComputedStyle(frames[0].document.querySelector('#framed')).display === 'none' ? 'child style applied' : 'child style missing';"));

        AssertPdfContains(result.PdfBytes, "child style applied");
    }

    [Fact]
    public async Task ExistingDirectoryFileBaseResolvesResourcesInsideThatDirectory() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-DirectoryBase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "message.js"), "document.querySelector('#result').textContent = 'directory resource loaded';");
        try {
            Uri baseUri = new(Path.GetFullPath(root));
            const string html = "<p id='result'>pending</p><script src='message.js'></script>";
            await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));

            HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromHtml(html, baseUri),
                readiness: new HtmlBrowserPdfReadiness(
                    skipLoadState: true,
                    function: "() => document.querySelector('#result').textContent === 'directory resource loaded'",
                    timeout: 5000)));

            AssertPdfContains(result.PdfBytes, "directory resource loaded");
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

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
                timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "window.open('/header-popup', '_blank'); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ExplicitBlankPopupRequestsWaitForOriginScopedHeaderInterception(int operation) {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        string script = operation switch {
            0 => "const popup = window.open('', '_blank'); popup.fetch('/blank-popup-fetch').then(response => response.text()).then(text => document.querySelector('#result').textContent = text); true",
            1 => "const popup = window.open('about:blank', '_blank'); popup.location = '/blank-popup-location'; true",
            2 => "const popup = window.open('about:blank', '_blank'); popup.location.href = '/blank-popup-location'; true",
            _ => "const popup = window.open('about:blank', '_blank'); popup.location.assign('/blank-popup-location'); true"
        };

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
    }

    [Fact]
    public async Task NestedPopupNavigationWaitsForOriginScopedHeaderInterception() {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.NestedPopupUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'nested popup authorized'",
                timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "window.open('/nested-parent', '_blank'); true"));

        AssertPdfContains(result.PdfBytes, "nested popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task PopupDocumentsStreamWhileTheFirstSubresourceReceivesOriginScopedHeaders() {
        await using LoopbackStreamingPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'streaming popup authorized'",
                timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "window.open('/streaming-popup', '_blank'); true"));

        AssertPdfContains(result.PdfBytes, "streaming popup authorized");
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

    [Theory]
    [InlineData("_self", false)]
    [InlineData("_parent", false)]
    [InlineData("_top", false)]
    [InlineData("reportFrame", true)]
    public async Task ExistingBrowsingContextsNavigateWithoutLosingTheDestination(string target, bool namedFrame) {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(namedFrame ? server.NamedContextUrl : server.ExistingContextUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'existing context authorized'",
                timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: $"window.open('/existing-context-destination', '{target}'); true"));

        AssertPdfContains(result.PdfBytes, "existing context authorized");
        Assert.Equal("popup-token", server.LastExistingContextToken);
    }

    [Fact]
    public async Task CrossOriginNamedContextNavigatesWithoutReadingItsWindowProxy() {
        await using LoopbackContentServer foreign = new("<html><body><p>foreign frame</p></body></html>");
        await using LoopbackPopupServer server = new(foreign.Url);
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.NamedContextUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'existing context authorized'",
                timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "window.open('/existing-context-destination', 'reportFrame'); true"));

        AssertPdfContains(result.PdfBytes, "existing context authorized");
        Assert.Equal("popup-token", server.LastExistingContextToken);
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

    [Fact]
    public async Task CaptureStyleSheetTimeoutReleasesTheBrowserLease() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));
        const string blockedHtml = "<html><body><p>blocked</p><script>document.querySelector = () => { while (true) { } };</script></body></html>";

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromHtml(blockedHtml),
                readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, timeout: 250),
                styleSheetContent: "body { color: black; }")));

        Assert.Contains("stylesheet injection", exception.Message, StringComparison.OrdinalIgnoreCase);
        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>style timeout recovered</p></body></html>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, timeout: 5000)));
        AssertPdfContains(recovered.PdfBytes, "style timeout recovered");
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
        private string? _lastExistingContextToken;
        private string? _lastSubmitAction;
        private readonly string _namedContextInitialUrl;

        internal LoopbackPopupServer(string? namedContextInitialUrl = null) {
            _namedContextInitialUrl = namedContextInitialUrl ?? "/existing-context-initial";
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            HeaderUrl = $"http://127.0.0.1:{port}/header-main";
            NestedPopupUrl = $"http://127.0.0.1:{port}/nested-main";
            NoOpenerHeaderUrl = $"http://127.0.0.1:{port}/header-noopener-main";
            StorageUrl = $"http://127.0.0.1:{port}/storage-main";
            ExistingContextUrl = $"http://127.0.0.1:{port}/existing-context-main";
            NamedContextUrl = $"http://127.0.0.1:{port}/named-context-main";
            DeclarativeAnchorUrl = $"http://127.0.0.1:{port}/declarative-anchor-main";
            DeclarativeFormUrl = $"http://127.0.0.1:{port}/declarative-form-main";
            DeclarativeFormOpenerUrl = $"http://127.0.0.1:{port}/declarative-form-opener-main";
            DeclarativeNamedUrl = $"http://127.0.0.1:{port}/declarative-named-main";
            DeclarativeSingleSubmitUrl = $"http://127.0.0.1:{port}/declarative-single-submit-main";
            DeclarativeSelfAnchorUrl = $"http://127.0.0.1:{port}/declarative-self-anchor-main";
            DeclarativeSelfFormUrl = $"http://127.0.0.1:{port}/declarative-self-form-main";
            DeclarativeExplicitSelfAnchorUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-anchor-main";
            DeclarativeExplicitSelfFormUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-form-main";
            DeclarativeExplicitSelfNativeFormUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-native-form-main";
            DeclarativeCancelledAnchorUrl = $"http://127.0.0.1:{port}/declarative-cancelled-anchor-main";
            DeclarativeWindowCancelledAnchorUrl = $"http://127.0.0.1:{port}/declarative-window-cancelled-anchor-main";
            DeclarativeReferrerPolicyUrl = $"http://127.0.0.1:{port}/declarative-referrer-policy-main";
            SiblingNamedContextUrl = $"http://127.0.0.1:{port}/sibling-named-context-main";
            _serverTask = ServeAsync();
        }

        internal string HeaderUrl { get; }
        internal string NestedPopupUrl { get; }
        internal string NoOpenerHeaderUrl { get; }
        internal string StorageUrl { get; }
        internal string ExistingContextUrl { get; }
        internal string NamedContextUrl { get; }
        internal string DeclarativeAnchorUrl { get; }
        internal string DeclarativeFormUrl { get; }
        internal string DeclarativeFormOpenerUrl { get; }
        internal string DeclarativeNamedUrl { get; }
        internal string DeclarativeSingleSubmitUrl { get; }
        internal string DeclarativeSelfAnchorUrl { get; }
        internal string DeclarativeSelfFormUrl { get; }
        internal string DeclarativeExplicitSelfAnchorUrl { get; }
        internal string DeclarativeExplicitSelfFormUrl { get; }
        internal string DeclarativeExplicitSelfNativeFormUrl { get; }
        internal string DeclarativeCancelledAnchorUrl { get; }
        internal string DeclarativeWindowCancelledAnchorUrl { get; }
        internal string DeclarativeReferrerPolicyUrl { get; }
        internal string SiblingNamedContextUrl { get; }
        internal string? LastPopupToken => Volatile.Read(ref _lastPopupToken);
        internal string? LastProtectedToken => Volatile.Read(ref _lastProtectedToken);
        internal string? LastPopupReferer => Volatile.Read(ref _lastPopupReferer);
        internal string? LastExistingContextToken => Volatile.Read(ref _lastExistingContextToken);

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
                    if (requestTarget.StartsWith("/nested-parent", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = "<script>window.open('/nested-child', '_blank');</script>";
                    } else if (requestTarget.StartsWith("/nested-child", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = "<p>nested child</p>";
                    } else if (requestTarget.StartsWith("/nested-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "nested popup authorized"
                            : "pending";
                    } else if (requestTarget.StartsWith("/nested-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => fetch('/nested-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/header-popup-noopener", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        Volatile.Write(ref _lastPopupReferer, LoopbackHtmlServer.ReadHeader(request, "Referer"));
                        body = "<script>fetch('/protected').then(response => response.text()).then(text => localStorage.setItem('popup-result', text));</script>";
                    } else if (requestTarget.StartsWith("/header-popup", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        Volatile.Write(ref _lastPopupReferer, LoopbackHtmlServer.ReadHeader(request, "Referer"));
                        if (requestTarget.Contains("action=approve", StringComparison.Ordinal)) {
                            Volatile.Write(ref _lastSubmitAction, "approve");
                        }
                        body = "<script>fetch('/protected').then(response => response.text()).then(text => opener.postMessage(text, '*'));</script>";
                    } else if (requestTarget.StartsWith("/blank-popup-fetch", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" ? "popup authorized" : "popup denied";
                    } else if (requestTarget.StartsWith("/blank-popup-location", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        string result = LastPopupToken == "popup-token" ? "popup authorized" : "popup denied";
                        body = $"<script>opener.postMessage('{result}', '*');</script>";
                    } else if (requestTarget.StartsWith("/protected", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token" ? "popup authorized" : "popup denied";
                    } else if (requestTarget.StartsWith("/popup-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token" ? "popup authorized" : "pending";
                    } else if (requestTarget.StartsWith("/submitter-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "popup authorized|" + (Volatile.Read(ref _lastSubmitAction) ?? "missing")
                            : "pending";
                    } else if (requestTarget.StartsWith("/storage-popup", StringComparison.Ordinal)) {
                        body = "<script>localStorage.setItem('observed', localStorage.getItem('token') || 'missing'); close();</script>";
                    } else if (requestTarget.StartsWith("/storage-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => document.querySelector('#result').textContent = localStorage.getItem('observed') || 'pending', 20);</script>";
                    } else if (requestTarget.StartsWith("/existing-context-destination", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastExistingContextToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = LastExistingContextToken == "popup-token"
                            ? "<p id='result'>existing context authorized</p>"
                            : "<p id='result'>existing context denied</p>";
                    } else if (requestTarget.StartsWith("/named-context-main", StringComparison.Ordinal)) {
                        body = $"<p id='result'>pending</p><iframe name='reportFrame' src='{System.Net.WebUtility.HtmlEncode(_namedContextInitialUrl)}'></iframe><script>setInterval(() => {{ try {{ document.querySelector('#result').textContent = frames.reportFrame.document.querySelector('#result').textContent; }} catch {{ }} }}, 20);</script>";
                    } else if (requestTarget.StartsWith("/sibling-named-context-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><iframe name='sourceFrame' srcdoc=\"<a href='/existing-context-destination' target='reportFrame'>open</a>\"></iframe><iframe name='reportFrame' src='about:blank'></iframe><script>setInterval(() => { try { const text = frames.reportFrame.document.querySelector('#result')?.textContent; if (text) document.querySelector('#result').textContent = text; } catch { } }, 20);</script>";
                    } else if (requestTarget.StartsWith("/existing-context-initial", StringComparison.Ordinal)
                        || requestTarget.StartsWith("/existing-context-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p>";
                    } else if (requestTarget.StartsWith("/header-noopener-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => document.querySelector('#result').textContent = localStorage.getItem('popup-result') || 'pending', 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank'>open</a><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-form-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/header-popup' method='post' target='_blank'><button type='submit'>open</button></form><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-form-opener-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/header-popup' method='post' target='_blank' rel='opener'><button type='submit'>open</button></form><script>addEventListener('message', event => document.querySelector('#result').textContent = event.data);</script>";
                    } else if (requestTarget.StartsWith("/declarative-named-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='reportWindow'>open</a><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-single-submit-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/wrong-popup' method='post' target='_blank'><button type='submit' name='action' value='approve' formaction='/header-popup' formmethod='get'>open</button></form><script>let submitCount = 0; document.addEventListener('submit', () => submitCount++); setInterval(() => fetch('/submitter-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text + '|' + submitCount), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-self-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/self-destination'>open</a>";
                    } else if (requestTarget.StartsWith("/declarative-self-form-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/self-destination' method='post'><button type='submit'>open</button></form>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-anchor-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><a href='/self-destination' target=''>open</a>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-form-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><form action='/self-destination' method='post' target='_blank'><button type='submit' formtarget=''>open</button></form>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-native-form-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><form action='/self-destination' method='post' target=''><button type='submit'>open</button></form>";
                    } else if (requestTarget.StartsWith("/declarative-cancelled-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank'>open</a><script>document.addEventListener('click', event => { event.preventDefault(); document.querySelector('#result').textContent = 'navigation cancelled'; });</script>";
                    } else if (requestTarget.StartsWith("/declarative-window-cancelled-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank'>open</a><script>window.addEventListener('click', event => { event.preventDefault(); document.querySelector('#result').textContent = 'window navigation cancelled'; });</script>";
                    } else if (requestTarget.StartsWith("/declarative-referrer-policy-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank' rel='opener' referrerpolicy='no-referrer'>open</a><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/self-destination", StringComparison.Ordinal)) {
                        body = "<p id='self-result'>self navigated</p>";
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

    private sealed class LoopbackStreamingPopupServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Task> _clients = new();
        private readonly TaskCompletionSource<bool> _protectedObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _serverTask;
        private string? _lastPopupToken;
        private string? _lastProtectedToken;
        private long _nextClient;

        internal LoopbackStreamingPopupServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/streaming-main";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastPopupToken => Volatile.Read(ref _lastPopupToken);
        internal string? LastProtectedToken => Volatile.Read(ref _lastProtectedToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    long id = Interlocked.Increment(ref _nextClient);
                    Task handling = HandleClientAsync(client);
                    _clients[id] = handling;
                    _ = handling.ContinueWith(
                        completed => {
                            _clients.TryRemove(id, out _);
                            _ = completed.Exception;
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                } catch (Exception ex) when (_cancellation.IsCancellationRequested
                    && (ex is ObjectDisposedException || ex is SocketException)) {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client) {
            using (client)
            using (NetworkStream stream = client.GetStream()) {
                try {
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellation.Token);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string requestTarget = request.Split(' ')[1];
                    if (requestTarget.StartsWith("/streaming-popup", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        const string streamingBody = "<script>fetch('/streaming-protected').then(response => response.text()).then(text => opener.postMessage(text, '*'));</script>";
                        byte[] bodyBytes = Encoding.UTF8.GetBytes(streamingBody);
                        byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n");
                        byte[] chunkHeader = Encoding.ASCII.GetBytes(bodyBytes.Length.ToString("X") + "\r\n");
                        await stream.WriteAsync(headers, 0, headers.Length, _cancellation.Token);
                        await stream.WriteAsync(chunkHeader, 0, chunkHeader.Length, _cancellation.Token);
                        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, _cancellation.Token);
                        await stream.WriteAsync(new byte[] { 13, 10 }, 0, 2, _cancellation.Token);
                        await stream.FlushAsync(_cancellation.Token);
                        await _protectedObserved.Task;
                        byte[] completed = Encoding.ASCII.GetBytes("0\r\n\r\n");
                        await stream.WriteAsync(completed, 0, completed.Length, _cancellation.Token);
                        return;
                    }

                    string body;
                    string contentType = "text/html; charset=utf-8";
                    if (requestTarget.StartsWith("/streaming-protected", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "streaming popup authorized"
                            : "streaming popup denied";
                        contentType = "text/plain; charset=utf-8";
                        _protectedObserved.TrySetResult(true);
                    } else {
                        body = "<p id='result'>pending</p><script>addEventListener('message', event => document.querySelector('#result').textContent = event.data);</script>";
                    }
                    byte[] bodyResponse = Encoding.UTF8.GetBytes(body);
                    byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bodyResponse.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length, _cancellation.Token);
                    await stream.WriteAsync(bodyResponse, 0, bodyResponse.Length, _cancellation.Token);
                } catch (Exception ex) when (_cancellation.IsCancellationRequested
                    && (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException || ex is IOException)) {
                    return;
                }
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _protectedObserved.TrySetCanceled();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            Task[] clients = _clients.Values.ToArray();
            if (clients.Length > 0) {
                try { await Task.WhenAll(clients); } catch (OperationCanceledException) { }
            }
            _cancellation.Dispose();
        }
    }
}
