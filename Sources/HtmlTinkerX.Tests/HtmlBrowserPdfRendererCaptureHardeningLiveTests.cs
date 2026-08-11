using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
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
}
