using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task PdfStreamingRejectsOversizedOutputAndKeepsTheWarmSlotReusable() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromHtml("<p>too large</p>"),
                maximumPdfBytes: 8)));
        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<p>stream recovered</p>")));

        Assert.Contains("configured limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        AssertPdfContains(recovered.PdfBytes, "stream recovered");
        Assert.Equal(1, renderer.GetMetricsSnapshot().BrowsersCreated);
    }

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
    public async Task ScopedRequestHeadersPreserveContextCookies() {
        await using LoopbackHtmlServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Correlation-Id"] = "with-cookie" },
            cookies: new[] { new HtmlBrowserPdfCookie("render-session", "authenticated", url: server.Url) });

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "URL invoice with-cookie render-session=authenticated");
    }

    [Fact]
    public async Task TrustedCallerProxyOwnsDnsForProxyOnlyHosts() {
        await using ProxyOnlyHostServer proxy = new();
        HtmlBrowserNetworkPolicy policy = new(allowPrivateNetworks: true);
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            proxy: proxy.Url,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl("http://renderer.proxy-only.invalid/invoice")));

        AssertPdfContains(result.PdfBytes, "proxy resolved page");
        Assert.Contains("renderer.proxy-only.invalid", proxy.LastRequestTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StableReadinessIncludesAttachedChildFrames() {
        const string html = "<html><body><iframe style='width:600px;height:100px' srcdoc=\"<p id='state'>child starts</p><script>setTimeout(() => document.querySelector('#state').textContent = 'child done', 300)</script>\"></iframe></body></html>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            readiness: new HtmlBrowserPdfReadiness(
                stable: true,
                stableMilliseconds: 200,
                pollMilliseconds: 25,
                timeout: 5000)));

        AssertPdfContains(result.PdfBytes, "child done");
    }

    [Fact]
    public async Task HtmlCredentialsAreScopedToTheDeclaredOrigin() {
        await using LoopbackContentServer foreignOrigin = new("foreign-resource");
        string html = $"<html><body data-loaded='0'><p id='main'>main-pending</p><script>document.querySelector('#main').textContent = localStorage.getItem('token') || 'main-missing'; function loaded() {{ document.body.dataset.loaded = '1'; }}</script><iframe src='{foreignOrigin.Url}' onload='loaded()'></iframe></body></html>";
        await using LoopbackContentServer declaredOrigin = new(html);
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromUrl(declaredOrigin.Url),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#main').textContent === 'origin-storage' && document.body.dataset.loaded === '1'",
                timeout: 30000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "origin-header" },
            localStorage: new System.Collections.Generic.Dictionary<string, string> { ["token"] = "origin-storage" });

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);
        AssertPdfContains(result.PdfBytes, "origin-storage");
        Assert.Equal("origin-header", declaredOrigin.LastRenderToken);
        Assert.Null(foreignOrigin.LastRenderToken);
    }

    [Fact]
    public async Task CrossOriginPageHeadersSurviveCaptureHeaderScoping() {
        await using LoopbackCorsHeaderServer foreignOrigin = new();
        string html = $"<html><body><p id='result'>pending</p><script>fetch('{foreignOrigin.Url}', {{ headers: {{ 'X-Render-Token': 'page-owned' }} }}).then(response => response.text()).then(text => document.querySelector('#result').textContent = text);</script></body></html>";
        await using LoopbackContentServer declaredOrigin = new(html);
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(declaredOrigin.Url),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'foreign authorized'",
                timeout: 30000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "capture-owned" }));

        AssertPdfContains(result.PdfBytes, "foreign authorized");
        Assert.Equal("capture-owned", declaredOrigin.LastRenderToken);
        Assert.Equal("page-owned", foreignOrigin.LastRenderToken);
    }

    [Fact]
    public async Task StorageSeedRunsOnlyInTheTopLevelDocument() {
        await using LoopbackContentServer origin = new("<html><body>child frame</body></html>");
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        string html = "<p id='result'>pending</p><script>localStorage.removeItem('token'); const frame = document.createElement('iframe'); frame.src = '/child'; frame.onload = () => document.querySelector('#result').textContent = localStorage.getItem('token') || 'not-restored'; document.body.appendChild(frame);</script>";
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml(html, new Uri(origin.Url)),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent !== 'pending'",
                timeout: 10000),
            localStorage: new System.Collections.Generic.Dictionary<string, string> { ["token"] = "initial-token" });

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "not-restored");
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
    public async Task HtmlBaseInjectionIgnoresHeadTextInsideCommentsAndScripts() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-PdfBase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(assets);
            Uri baseUri = new Uri(assets + Path.DirectorySeparatorChar);
            string html = "<!-- <head>comment-only</head> --><script>const sample = '<head>script-only</head>';</script><p id='base'>pending</p>";
            HtmlBrowserNetworkPolicy policy = new(
                allowFileAccess: true,
                allowedFileDirectories: new[] { root });
            await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
                maximumBrowserInstances: 1,
                networkPolicy: policy));

            HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromHtml(html, baseUri),
                beforeCaptureScript: "document.querySelector('#base').textContent = document.baseURI.endsWith('/assets/') ? 'base-resolved' : document.baseURI;"));

            AssertPdfContains(result.PdfBytes, "base-resolved");
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HtmlDocumentRouteOnlyFulfillsTheInitialNavigation() {
        await using LoopbackContentServer origin = new("server-location-response");
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        string html = "<p id='result'>pending</p><script>fetch(location.href).then(r => r.text()).then(value => document.querySelector('#result').textContent = value);</script>";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html, new Uri(origin.Url)),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent.includes('server-location-response')",
                timeout: 10000)));

        AssertPdfContains(result.PdfBytes, "server-location-response");
    }

    [Fact]
    public async Task ReadinessTimeoutDoesNotLimitInitialNavigation() {
        await using LoopbackContentServer origin = new("<p>navigation completed</p>", TimeSpan.FromMilliseconds(1500));
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromUrl(origin.Url),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, selector: "p", timeout: 1000),
            navigationTimeout: 5000);

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);

        AssertPdfContains(result.PdfBytes, "navigation completed");
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
    public async Task PreWarmReplacesExpiredIdleBrowsersBeforeCountingTheMinimum() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            minimumBrowserInstances: 1,
            maximumBrowserInstances: 1,
            maximumBrowserAge: TimeSpan.FromMilliseconds(100)));

        await renderer.PreWarmAsync();
        await Task.Delay(150);
        Assert.Equal(0, renderer.GetMetricsSnapshot().IdleBrowsers);
        await renderer.PreWarmAsync();

        HtmlBrowserPdfRendererMetrics metrics = renderer.GetMetricsSnapshot();
        Assert.Equal(2, metrics.BrowsersCreated);
        Assert.Equal(1, metrics.BrowsersRecycled);
        Assert.Equal(1, metrics.IdleBrowsers);
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
    public async Task ScopedHeadersPreserveTheAllowedHostPolicyAcrossRedirects() {
        await using LoopbackRedirectServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "localhost" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "scoped" }));

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

    [Fact]
    public async Task ScopedRequestHeadersDoNotBufferEventStreams() {
        await using LoopbackStreamingServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.Url),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#state').textContent === 'streaming-ready'",
                timeout: 5000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "stream-token" }));

        AssertPdfContains(result.PdfBytes, "streaming-ready");
        Assert.Equal("stream-token", server.EventStreamToken);
    }

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
                    string cookie = ReadHeader(request, "Cookie") ?? "missing";
                    string body = $"<html><body><h1>URL invoice {System.Net.WebUtility.HtmlEncode(correlation)} {System.Net.WebUtility.HtmlEncode(cookie)}</h1></body></html>";
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
        private readonly ConcurrentDictionary<Task, byte> _connections = new();
        private readonly Task _serverTask;
        private readonly string _body;
        private readonly TimeSpan _responseDelay;
        private string? _lastRenderToken;
        private int _requestCount;

        internal LoopbackContentServer(string body, TimeSpan? responseDelay = null) {
            _body = body;
            _responseDelay = responseDelay ?? TimeSpan.Zero;
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/origin";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastRenderToken => Volatile.Read(ref _lastRenderToken);
        internal int RequestCount => Volatile.Read(ref _requestCount);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TrackConnection(HandleConnectionAsync(await _listener.AcceptTcpClientAsync()));
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
                byte[] buffer = new byte[8192];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellation.Token);
                if (read == 0) return;
                Interlocked.Increment(ref _requestCount);
                string request = Encoding.ASCII.GetString(buffer, 0, read);
                string? token = LoopbackHtmlServer.ReadHeader(request, "X-Render-Token");
                if (token != null) Volatile.Write(ref _lastRenderToken, token);
                if (_responseDelay > TimeSpan.Zero) await Task.Delay(_responseDelay, _cancellation.Token);
                byte[] bodyBytes = Encoding.UTF8.GetBytes(_body);
                byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers, 0, headers.Length, _cancellation.Token);
                await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, _cancellation.Token);
            }
        }

        private void TrackConnection(Task connection) {
            _connections[connection] = 0;
            _ = connection.ContinueWith(
                completed => _connections.TryRemove(completed, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            try { await Task.WhenAll(_connections.Keys); } catch (OperationCanceledException) { } catch (ObjectDisposedException) { } catch (IOException) { } catch (SocketException) { }
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

    private sealed class ProxyOnlyHostServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private string? _lastRequestTarget;

        internal ProxyOnlyHostServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastRequestTarget => Volatile.Read(ref _lastRequestTarget);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string firstLine = request.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                    Volatile.Write(ref _lastRequestTarget, firstLine);
                    byte[] body = Encoding.UTF8.GetBytes("<html><body><p>proxy resolved page</p></body></html>");
                    byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, 0, headers.Length);
                    await stream.WriteAsync(body, 0, body.Length);
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

    private sealed class LoopbackStreamingServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private string? _eventStreamToken;

        internal LoopbackStreamingServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? EventStreamToken => Volatile.Read(ref _eventStreamToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = HandleAsync(client);
                } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                    return;
                } catch (SocketException) when (_cancellation.IsCancellationRequested) {
                    return;
                }
            }
        }

        private async Task HandleAsync(TcpClient client) {
            using (client)
            using (NetworkStream stream = client.GetStream()) {
                try {
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    if (request.StartsWith("GET /events", StringComparison.Ordinal)) {
                        Volatile.Write(ref _eventStreamToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-cache\r\nConnection: keep-alive\r\n\r\n");
                        byte[] message = Encoding.UTF8.GetBytes("data: streaming-ready\n\n");
                        await stream.WriteAsync(headers, 0, headers.Length);
                        await stream.WriteAsync(message, 0, message.Length);
                        await stream.FlushAsync();
                        await Task.Delay(Timeout.Infinite, _cancellation.Token);
                        return;
                    }
                    const string html = "<html><body><p id='state'>pending</p><script>const source = new EventSource('/events'); source.onmessage = event => { document.querySelector('#state').textContent = event.data; source.close(); };</script></body></html>";
                    byte[] body = Encoding.UTF8.GetBytes(html);
                    byte[] responseHeaders = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(responseHeaders, 0, responseHeaders.Length);
                    await stream.WriteAsync(body, 0, body.Length);
                } catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) {
                } catch (ObjectDisposedException) {
                } catch (IOException) {
                } catch (SocketException) {
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

    private sealed class LoopbackCorsHeaderServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentDictionary<Task, byte> _connections = new();
        private readonly Task _serverTask;
        private string? _lastRenderToken;

        internal LoopbackCorsHeaderServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/foreign";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastRenderToken => Volatile.Read(ref _lastRenderToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Task connection = HandleConnectionAsync(client);
                    _connections[connection] = 0;
                    _ = connection.ContinueWith(
                        completed => _connections.TryRemove(completed, out _),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
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
                byte[] buffer = new byte[8192];
                using MemoryStream requestBytes = new();
                string request;
                do {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellation.Token);
                    if (read == 0) return;
                    requestBytes.Write(buffer, 0, read);
                    if (requestBytes.Length > 65536) throw new InvalidDataException("Loopback CORS request headers exceeded 64 KiB.");
                    request = Encoding.ASCII.GetString(requestBytes.GetBuffer(), 0, checked((int)requestBytes.Length));
                } while (!request.Contains("\r\n\r\n", StringComparison.Ordinal));
                string? origin = LoopbackHtmlServer.ReadHeader(request, "Origin");
                bool preflight = request.StartsWith("OPTIONS ", StringComparison.Ordinal);
                if (!preflight) {
                    Volatile.Write(ref _lastRenderToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                }
                byte[] body = preflight ? Array.Empty<byte>() : Encoding.UTF8.GetBytes("foreign authorized");
                StringBuilder headers = new();
                headers.Append("HTTP/1.1 200 OK\r\n")
                    .Append("Content-Type: text/plain; charset=utf-8\r\n")
                    .Append("Access-Control-Allow-Headers: X-Render-Token\r\n")
                    .Append("Access-Control-Allow-Methods: GET, OPTIONS\r\n");
                if (origin != null) headers.Append("Access-Control-Allow-Origin: ").Append(origin).Append("\r\n");
                headers.Append("Content-Length: ").Append(body.Length).Append("\r\nConnection: close\r\n\r\n");
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length, _cancellation.Token);
                if (body.Length > 0) await stream.WriteAsync(body, 0, body.Length, _cancellation.Token);
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            try { await Task.WhenAll(_connections.Keys); } catch (OperationCanceledException) { } catch (ObjectDisposedException) { } catch (IOException) { } catch (SocketException) { }
            _cancellation.Dispose();
        }
    }

}
