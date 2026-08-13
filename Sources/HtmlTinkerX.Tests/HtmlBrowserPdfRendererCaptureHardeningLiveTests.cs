using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task VisualMaskBlocksChildFrameNavigationUntilPrintActionCompletes() {
        await using LoopbackContentServer server = new("<p id='replacement'>replacement sensitive value</p>");
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync($"<iframe srcdoc=\"<meta http-equiv='refresh' content='0.5;url={server.Url}'><p id='secret'>original sensitive value</p>\"></iframe>");
        IFrame child = session.Page.Frames[1];

        string? state = null;
        try {
            state = await HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
                session.Page,
                maskSensitiveElements: false,
                maskSelectors: new[] { "#secret" },
                maskColor: "#000000",
                action: async () => {
                    await Task.Delay(1500);
                    return await child.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#secret')).visibility + '|' + document.body.textContent.trim()");
                },
                cancellationToken: CancellationToken.None,
                freezePageScriptsDuringAction: true);
        } catch (PlaywrightException) { }

        Assert.Equal(0, server.RequestCount);
        if (state != null) Assert.Equal("hidden|original sensitive value", state);
    }

    [Fact]
    public async Task OrdinaryCapturePreservesBeforePrintHandlers() {
        const string html = @"<p id='result'>beforeprint pending</p><script>
            addEventListener('beforeprint', () => document.querySelector('#result').textContent = 'ordinary beforeprint ran');
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html)));

        AssertPdfContains(result.PdfBytes, "ordinary beforeprint ran");
    }

    [Fact]
    public async Task BrowserPdfMaskRedactsTextAfterPageScriptsReplaceDomPrimitives() {
        const string html = @"<p>public artifact marker</p><p id='secret'>sensitive artifact value</p><script>
            document.querySelectorAll = () => [];
            Document.prototype.querySelectorAll = () => [];
            Element.prototype.querySelectorAll = () => [];
            Document.prototype.createElement = () => { throw new Error('page-owned createElement'); };
            Element.prototype.getBoundingClientRect = () => ({ left: 0, top: 0, width: 0, height: 0 });
            CSSStyleDeclaration.prototype.setProperty = () => {};
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            pdfOptions: new HtmlBrowserPdfOptions(maskSelectors: new[] { "#secret" })));

        AssertPdfContains(result.PdfBytes, "public artifact marker");
        AssertPdfDoesNotContain(result.PdfBytes, "sensitive artifact value");
    }

    [Fact]
    public async Task BrowserPdfMaskCannotBeRemovedByPageBeforePrintHandlers() {
        const string html = @"<p>public beforeprint marker</p><p id='secret'>beforeprint sensitive value</p><script>
            addEventListener('beforeprint', () => {
                document.querySelector('#secret').style.setProperty('visibility', 'visible', 'important');
                document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').forEach(element => element.remove());
            });
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            pdfOptions: new HtmlBrowserPdfOptions(maskSelectors: new[] { "#secret" })));

        AssertPdfContains(result.PdfBytes, "public beforeprint marker");
        AssertPdfDoesNotContain(result.PdfBytes, "beforeprint sensitive value");
    }

    [Fact]
    public async Task BrowserPdfMaskCannotBeRemovedByMutationObservers() {
        const string html = @"<p>public observer marker</p><p id='secret'>observer sensitive value</p><script>
            new MutationObserver(() => {
                document.querySelector('#secret').style.setProperty('visibility', 'visible', 'important');
                document.querySelectorAll('[data-htmltinkerx-visual-mask-overlay]').forEach(element => element.remove());
            }).observe(document.documentElement, { subtree: true, childList: true, attributes: true });
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            pdfOptions: new HtmlBrowserPdfOptions(maskSelectors: new[] { "#secret" })));

        AssertPdfContains(result.PdfBytes, "public observer marker");
        AssertPdfDoesNotContain(result.PdfBytes, "observer sensitive value");
    }

    [Fact]
    public async Task CaptureStyleSheetIgnoresPageOwnedDomMethodOverrides() {
        const string html = @"<p>public stylesheet marker</p><p id='secret'>stylesheet sensitive value</p><script>
            document.querySelector = () => ({ textContent: '' });
            Document.prototype.querySelector = () => ({ textContent: '' });
            Document.prototype.createElement = () => ({ setAttribute() {}, textContent: '' });
            Element.prototype.prepend = () => {};
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            styleSheetContent: "#secret { display: none !important; }"));

        AssertPdfContains(result.PdfBytes, "public stylesheet marker");
        AssertPdfDoesNotContain(result.PdfBytes, "stylesheet sensitive value");
    }

    [Fact]
    public async Task CaptureStyleSheetDoesNotRewriteIdenticalContentAfterReadiness() {
        const string html = "<p id='result'>capture style pending</p><p id='secret'>stable stylesheet sensitive value</p>";
        const string beforeCaptureScript = @"const marker = document.querySelector('#result');
            const style = document.querySelector('style[data-htmltinkerx-pdf-capture-style]');
            marker.textContent = 'capture style stable';
            new MutationObserver(() => marker.textContent = 'capture style rewritten')
                .observe(style, { subtree: true, childList: true, characterData: true });";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 200),
            styleSheetContent: "#secret { display: none !important; }",
            beforeCaptureScript: beforeCaptureScript));

        AssertPdfContains(result.PdfBytes, "capture style stable");
        AssertPdfDoesNotContain(result.PdfBytes, "capture style rewritten");
        AssertPdfDoesNotContain(result.PdfBytes, "stable stylesheet sensitive value");
    }

    [Fact]
    public async Task CaptureStyleSheetCannotBeRemovedByBeforePrintHandlers() {
        const string html = @"<p>public beforeprint style marker</p><p id='secret'>beforeprint style sensitive value</p><script>
            addEventListener('beforeprint', () => {
                document.querySelector('style[data-htmltinkerx-pdf-capture-style]')?.remove();
            });
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            styleSheetContent: "#secret { display: none !important; }"));

        AssertPdfContains(result.PdfBytes, "public beforeprint style marker");
        AssertPdfDoesNotContain(result.PdfBytes, "beforeprint style sensitive value");
    }

    [Fact]
    public async Task FinalCaptureStyleSheetCannotBeRemovedByMutationObservers() {
        const string html = @"<p>public protected style marker</p><p id='secret'>observer style sensitive value</p><script>
            new MutationObserver(() => {
                document.querySelector('style[data-htmltinkerx-pdf-capture-style]')?.remove();
            }).observe(document.documentElement, { subtree: true, childList: true });
        </script>";
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, delayMilliseconds: 200),
            styleSheetContent: "#secret { display: none !important; }"));

        AssertPdfContains(result.PdfBytes, "public protected style marker");
        AssertPdfDoesNotContain(result.PdfBytes, "observer style sensitive value");
    }
}
