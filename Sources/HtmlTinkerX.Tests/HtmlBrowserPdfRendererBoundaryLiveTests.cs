using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using UglyToad.PdfPig;
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
    public async Task VisualMaskIgnoresPageOwnedSelectorOverrides() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<input id='secret' type='password' style='width:140px;height:40px'>");
        IElementHandle secret = (await session.Page.QuerySelectorAsync("#secret"))!;
        await session.Page.EvaluateAsync(@"() => {
            document.querySelectorAll = () => [];
            Document.prototype.querySelectorAll = () => [];
            Element.prototype.querySelectorAll = () => [];
            ShadowRoot.prototype.querySelectorAll = () => [];
            Document.prototype.createElement = () => { throw new Error('page-owned createElement'); };
            Element.prototype.getBoundingClientRect = () => ({ left: 0, top: 0, width: 0, height: 0 });
            CSSStyleDeclaration.prototype.setProperty = () => {};
        }");

        string masked = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            session.Page,
            maskSensitiveElements: true,
            maskSelectors: new[] { "#secret" },
            maskColor: "#000000",
            action: () => secret.EvaluateAsync<string>("element => getComputedStyle(element).visibility"),
            cancellationToken: CancellationToken.None);

        Assert.Equal("hidden", masked);
        Assert.Equal("visible|width:140px;height:40px", await secret.EvaluateAsync<string>(
            "element => getComputedStyle(element).visibility + '|' + element.getAttribute('style')"));
    }

    [Fact]
    public async Task VisualMaskAppliesToChildFramesAndRestoresTheirInlineStyles() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<iframe srcdoc=\"<input id='secret' style='width:120px;height:30px'>\"></iframe>");
        IFrame child = session.Page.Frames.Single(frame => !ReferenceEquals(frame, session.Page.MainFrame));

        string masked = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
            session.Page,
            maskSensitiveElements: false,
            maskSelectors: new[] { "#secret" },
            maskColor: "#000000",
            action: () => child.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#secret')).visibility"),
            cancellationToken: CancellationToken.None);

        Assert.Equal("hidden", masked);
        Assert.Equal("visible|width:120px;height:30px", await child.EvaluateAsync<string>(
            "() => { const secret = document.querySelector('#secret'); return getComputedStyle(secret).visibility + '|' + secret.getAttribute('style'); }"));
    }

    private static void AssertPdfDoesNotContain(byte[] bytes, string unexpectedText) {
        using MemoryStream stream = new(bytes, writable: false);
        using PdfDocument document = PdfDocument.Open(stream);
        string text = string.Join(" ", document.GetPages().Select(page => page.Text));
        Assert.DoesNotContain(unexpectedText, text, StringComparison.OrdinalIgnoreCase);
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
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task ExplicitBlankPopupRequestsWaitForOriginScopedHeaderInterception(int operation) {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));
        string script = operation switch {
            0 => "const popup = window.open('', '_blank'); popup.fetch('/blank-popup-fetch').then(response => response.text()).then(text => document.querySelector('#result').textContent = text); true",
            1 => "const popup = window.open('about:blank', '_blank'); popup.location = '/blank-popup-location'; true",
            2 => "const popup = window.open('about:blank', '_blank'); popup.location.href = '/blank-popup-location'; true",
            3 => "const popup = window.open('about:blank', '_blank'); popup.location.assign('/blank-popup-location'); true",
            4 => $"const popup = window.open('', '_blank'); popup.document.write(`<iframe src='{server.BlankPopupResourceUrl}'></iframe>`); true",
            5 => $"const popup = window.open('', '_blank'); popup.document.body.innerHTML = `<iframe src='{server.BlankPopupResourceUrl}'></iframe>`; true",
            6 => $"const popup = window.open('', '_blank'); const frame = popup.document.createElement('iframe'); frame.src = '{server.BlankPopupResourceUrl}'; popup.document.body.appendChild(frame); true",
            _ => $"const popup = window.open('', '_blank'); const frame = popup.document.createElement('iframe'); popup.document.body.append(frame); if (frame.contentDocument !== frame.contentDocument || frame.contentWindow.document !== frame.contentDocument) throw new Error('child document identity changed'); frame.contentDocument.body.innerHTML = `<img src='{server.BlankPopupResourceUrl}?source=frame-content-document'>`; frame.contentWindow.document.body.insertAdjacentHTML('beforeend', `<img src='{server.BlankPopupResourceUrl}?source=frame-content-window'>`); frame.contentWindow.fetch('{server.BlankPopupResourceUrl}?source=frame-window-fetch'); frame.contentWindow.navigator.sendBeacon('{server.BlankPopupResourceUrl}?source=frame-window-beacon', 'audit'); true"
        };

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: operation >= 4
                ? new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 2000)
                : new HtmlBrowserPdfReadiness(
                    skipLoadState: true,
                    function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                    timeout: 10000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));
        Assert.True(operation < 4 || server.BlankPopupResourceRequests > 0, "The staged popup resource request was not observed by the origin server.");
        if (operation == 7) {
            Assert.Equal(1, server.BlankPopupSourceRequests("frame-content-document"));
            Assert.Equal(1, server.BlankPopupSourceRequests("frame-content-window"));
            Assert.Equal(1, server.BlankPopupSourceRequests("frame-window-fetch"));
            Assert.Equal(1, server.BlankPopupSourceRequests("frame-window-beacon"));
        }
        Assert.Equal("popup-token", server.LastPopupToken);
        if (operation < 4) AssertPdfContains(result.PdfBytes, "popup authorized");
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
    public async Task PageMonkeypatchCannotForgeAnExistingNamedContext() {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.ExistingContextUrl),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 1000),
            headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: @"document.querySelectorAll = () => [{ getAttribute: () => 'forgedFrame', contentDocument: document }];
                Object.defineProperty = () => { throw new Error('page defineProperty'); };
                Object.getOwnPropertyDescriptor = () => { throw new Error('page descriptor'); };
                Object.getPrototypeOf = () => { throw new Error('page prototype'); };
                Reflect.apply = () => { throw new Error('page apply'); };
                Reflect.construct = () => { throw new Error('page construct'); };
                Reflect.get = () => { throw new Error('page get'); };
                Reflect.set = () => { throw new Error('page set'); };
                window.open('/existing-context-destination', 'forgedFrame');
                true"));

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
            beforeCaptureScript: "localStorage.clear(); sessionStorage.clear(); setTimeout(() => location.reload(), 0); true"));

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
    public async Task PreparationTimeoutRecyclesTheBrowserWhenReadinessIsUnlimited() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromHtml("<html><body><p>blocked</p></body></html>"),
                readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, timeout: 0),
                styleSheetContent: "body { color: black; }",
                beforeCaptureScript: "new Promise(resolve => setTimeout(resolve, 1000))",
                preparationTimeout: 50,
                beforeCaptureScriptTimeout: 5000)));

        Assert.Contains("preparation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(renderer.GetMetricsSnapshot().BrowsersRecycled >= 1);
        HtmlBrowserPdfResult recovered = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<html><body><p>style timeout recovered</p></body></html>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, timeout: 5000)));
        AssertPdfContains(recovered.PdfBytes, "style timeout recovered");
    }

}
