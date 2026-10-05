using Microsoft.Playwright;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    // Mark hidden content and capture its document identity in the same execution context.
    private const string CaptureRenderedDocumentScript = "respectHidden => { if (respectHidden) ("
        + MarkRenderedHiddenElementsScript + ")(); return { url: location.href, title: document.title, "
        + "documentUrl: performance.getEntriesByType('navigation')[0]?.name ?? null, "
        + "html: (document.doctype ? new XMLSerializer().serializeToString(document.doctype) : '') + document.documentElement.outerHTML }; }";

    private static async Task<JsonElement> CaptureRenderedDocumentAsync(IPage page, HtmlCrawlOptions options, CancellationToken cancellationToken) {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);
        CancellationToken token = deadline.Token;
        try {
            while (true) {
                try {
                    return await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                        () => page.EvaluateAsync<JsonElement>(CaptureRenderedDocumentScript, options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden),
                        static () => Task.CompletedTask, token).ConfigureAwait(false);
                } catch (PlaywrightException ex) when (!page.IsClosed && ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)) {
                    // A navigation can replace the document after the last interaction.
                    await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                        () => page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = options.Timeout }),
                        static () => Task.CompletedTask, token).ConfigureAwait(false);
                }
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
            throw new TimeoutException($"Rendered document capture exceeded its {options.Timeout} ms timeout.");
        }
    }
}
