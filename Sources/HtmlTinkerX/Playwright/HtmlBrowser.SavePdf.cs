namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Browser PDF operations for pages already owned by a caller.</summary>
public static partial class HtmlBrowser {
    /// <summary>Saves an already-loaded Chromium page as a PDF.</summary>
    /// <param name="page">The caller-owned Playwright page. Cancellation closes this page to abort Chromium printing.</param>
    /// <param name="path">Destination PDF path.</param>
    /// <param name="options">Immutable Chromium print options.</param>
    /// <param name="readiness">Optional readiness checks. Omit when the caller has already prepared the page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SavePagePdfAsync(
        IPage page,
        string path,
        HtmlBrowserPdfOptions? options = null,
        HtmlBrowserPdfReadiness? readiness = null,
        CancellationToken cancellationToken = default) {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("PDF output path is required.", nameof(path));

        options ??= new HtmlBrowserPdfOptions();
        if (readiness != null) {
            await HtmlBrowserPdfCapture.WaitForReadinessAsync(page, readiness, cancellationToken).ConfigureAwait(false);
        }

        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
        PagePdfOptions pageOptions = HtmlBrowserPdfCapture.CreatePageOptions(options, fullPath);
        await ExecuteWithTemporaryVisualMaskAsync(
            page,
            options.MaskSensitiveElements,
            options.MaskSelectors,
            options.MaskColor,
            async () => {
                await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    () => page.PdfAsync(pageOptions),
                    () => page.CloseAsync(),
                    cancellationToken).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns PDF bytes for an already-loaded Chromium page.</summary>
    /// <param name="page">The caller-owned Playwright page. Cancellation closes this page to abort Chromium printing.</param>
    /// <param name="options">Immutable Chromium print options.</param>
    /// <param name="readiness">Optional readiness checks. Omit when the caller has already prepared the page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated PDF bytes.</returns>
    public static async Task<byte[]> GetPagePdfAsync(
        IPage page,
        HtmlBrowserPdfOptions? options = null,
        HtmlBrowserPdfReadiness? readiness = null,
        CancellationToken cancellationToken = default) {
        if (page == null) throw new ArgumentNullException(nameof(page));

        options ??= new HtmlBrowserPdfOptions();
        if (readiness != null) {
            await HtmlBrowserPdfCapture.WaitForReadinessAsync(page, readiness, cancellationToken).ConfigureAwait(false);
        }

        PagePdfOptions pageOptions = HtmlBrowserPdfCapture.CreatePageOptions(options);
        return await ExecuteWithTemporaryVisualMaskAsync(
            page,
            options.MaskSensitiveElements,
            options.MaskSelectors,
            options.MaskColor,
            () => HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => page.PdfAsync(pageOptions),
                () => page.CloseAsync(),
                cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }
}
