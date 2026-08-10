using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Saves a PDF of the specified page URL to disk.
    /// </summary>
    public static async Task SavePagePdfAsync(
            string url,
            string path,
            HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
            bool clean = false,
            int delayMs = 0,
            string? selector = null,
            bool landscape = false,
            bool printBackground = false,
            PdfPageFormat? format = null,
            string? width = null,
            string? height = null,
            string? marginTop = null,
            string? marginRight = null,
            string? marginBottom = null,
            string? marginLeft = null,
            string? pageRanges = null,
            float? scale = null,
            bool displayHeaderFooter = false,
            string? headerTemplate = null,
            string? footerTemplate = null,
            bool preferCssPageSize = false,
            bool outline = false,
        bool tagged = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        CancellationToken cancellationToken = default,
        bool maskSensitiveElements = false,
        IEnumerable<string>? maskSelectors = null,
        string? maskColor = null) {
        EnsurePdfBrowserSupported(browser);
        HtmlBrowserLaunchOptions launchOptions = HtmlBrowserLaunchOptions.FromLegacyParameters(
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            proxy: proxy,
            proxyUsername: proxyUsername,
            proxyPassword: proxyPassword);

        await SavePagePdfAsync(
            url,
            path,
            launchOptions,
            delayMs,
            selector,
            landscape,
            printBackground,
            format,
            width,
            height,
            marginTop,
            marginRight,
            marginBottom,
            marginLeft,
            pageRanges,
            scale,
            displayHeaderFooter,
            headerTemplate,
            footerTemplate,
            preferCssPageSize,
            outline,
            tagged,
            cancellationToken,
            maskSensitiveElements,
            maskSelectors,
            maskColor).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves a PDF of the specified page URL to disk using reusable browser launch options.
    /// </summary>
    public static async Task SavePagePdfAsync(
        string url,
        string path,
        HtmlBrowserLaunchOptions launchOptions,
        int delayMs = 0,
        string? selector = null,
        bool landscape = false,
        bool printBackground = false,
        PdfPageFormat? format = null,
        string? width = null,
        string? height = null,
        string? marginTop = null,
        string? marginRight = null,
        string? marginBottom = null,
        string? marginLeft = null,
        string? pageRanges = null,
        float? scale = null,
        bool displayHeaderFooter = false,
        string? headerTemplate = null,
        string? footerTemplate = null,
        bool preferCssPageSize = false,
        bool outline = false,
        bool tagged = false,
        CancellationToken cancellationToken = default,
        bool maskSensitiveElements = false,
        IEnumerable<string>? maskSelectors = null,
        string? maskColor = null) {
        if (launchOptions == null) {
            throw new ArgumentNullException(nameof(launchOptions));
        }
        EnsurePdfBrowserSupported(launchOptions.Browser);

        if (delayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be zero or positive.");
        }

        await using HtmlBrowserSession session = await OpenSessionAsync(url, launchOptions, cancellationToken).ConfigureAwait(false);

        await SavePagePdfAsync(
            session.Page,
            path,
            delayMs,
            selector,
            landscape,
            printBackground,
            format,
            width,
            height,
            marginTop,
            marginRight,
            marginBottom,
            marginLeft,
            pageRanges,
            scale,
            displayHeaderFooter,
            headerTemplate,
            footerTemplate,
            preferCssPageSize,
            outline,
            tagged,
            cancellationToken,
            maskSensitiveElements,
            maskSelectors,
            maskColor).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves a PDF from an already loaded page.
    /// </summary>
    public static async Task SavePagePdfAsync(
        IPage page,
        string path,
        int delayMs = 0,
        string? selector = null,
        bool landscape = false,
        bool printBackground = false,
        PdfPageFormat? format = null,
        string? width = null,
        string? height = null,
        string? marginTop = null,
        string? marginRight = null,
        string? marginBottom = null,
        string? marginLeft = null,
        string? pageRanges = null,
        float? scale = null,
        bool displayHeaderFooter = false,
        string? headerTemplate = null,
        string? footerTemplate = null,
        bool preferCssPageSize = false,
        bool outline = false,
        bool tagged = false,
        CancellationToken cancellationToken = default,
        bool maskSensitiveElements = false,
        IEnumerable<string>? maskSelectors = null,
        string? maskColor = null) {
        if (delayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be zero or positive.");
        }
        if (!string.IsNullOrEmpty(selector)) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForSelectorAsync(selector!, new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        if (delayMs > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForTimeoutAsync(delayMs);
        }

        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
        var options = new PagePdfOptions {
            Path = fullPath,
            Landscape = landscape,
            PrintBackground = printBackground,
            Format = format switch {
                PdfPageFormat.A0 => "A0",
                PdfPageFormat.A1 => "A1",
                PdfPageFormat.A2 => "A2",
                PdfPageFormat.A3 => "A3",
                PdfPageFormat.A4 => "A4",
                PdfPageFormat.A5 => "A5",
                PdfPageFormat.A6 => "A6",
                PdfPageFormat.Letter => "Letter",
                PdfPageFormat.Legal => "Legal",
                PdfPageFormat.Tabloid => "Tabloid",
                PdfPageFormat.Ledger => "Ledger",
                _ => null
            },
            Width = width,
            Height = height,
            PageRanges = pageRanges,
            Scale = scale,
            DisplayHeaderFooter = displayHeaderFooter,
            HeaderTemplate = headerTemplate,
            FooterTemplate = footerTemplate,
            PreferCSSPageSize = preferCssPageSize,
            Outline = outline,
            Tagged = tagged
        };
        if (marginTop != null || marginRight != null || marginBottom != null || marginLeft != null) {
            options.Margin = new Margin {
                Top = marginTop,
                Right = marginRight,
                Bottom = marginBottom,
                Left = marginLeft
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        await ExecuteWithTemporaryVisualMaskAsync(
            page,
            maskSensitiveElements,
            maskSelectors,
            maskColor,
            async () => {
                await ExecutePdfOperationWithCancellationAsync(page, () => page.PdfAsync(options), cancellationToken).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports a PDF from an already loaded page and returns the bytes.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="delayMs">Optional delay in milliseconds before generating the PDF.</param>
    /// <param name="selector">Optional selector that must appear before printing.</param>
    /// <param name="landscape">Print pages in landscape orientation.</param>
    /// <param name="printBackground">Include background graphics.</param>
    /// <param name="format">Standard page size to use.</param>
    /// <param name="width">Page width.</param>
    /// <param name="height">Page height.</param>
    /// <param name="marginTop">Top margin.</param>
    /// <param name="marginRight">Right margin.</param>
    /// <param name="marginBottom">Bottom margin.</param>
    /// <param name="marginLeft">Left margin.</param>
    /// <param name="pageRanges">Page ranges to print.</param>
    /// <param name="scale">Scaling factor.</param>
    /// <param name="displayHeaderFooter">Display header and footer.</param>
    /// <param name="headerTemplate">HTML template for the header.</param>
    /// <param name="footerTemplate">HTML template for the footer.</param>
    /// <param name="preferCssPageSize">Use @page size from CSS.</param>
    /// <param name="outline">Create tagged outline.</param>
    /// <param name="tagged">Produce tagged PDF.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="maskSensitiveElements">Mask common sensitive fields before generating the PDF.</param>
    /// <param name="maskSelectors">Additional CSS selectors to mask before generating the PDF.</param>
    /// <param name="maskColor">CSS color used for masked elements.</param>
    /// <returns>Generated PDF bytes.</returns>
    public static async Task<byte[]> GetPagePdfAsync(
        IPage page,
        int delayMs = 0,
        string? selector = null,
        bool landscape = false,
        bool printBackground = false,
        PdfPageFormat? format = null,
        string? width = null,
        string? height = null,
        string? marginTop = null,
        string? marginRight = null,
        string? marginBottom = null,
        string? marginLeft = null,
        string? pageRanges = null,
        float? scale = null,
        bool displayHeaderFooter = false,
        string? headerTemplate = null,
        string? footerTemplate = null,
        bool preferCssPageSize = false,
        bool outline = false,
        bool tagged = false,
        CancellationToken cancellationToken = default,
        bool maskSensitiveElements = false,
        IEnumerable<string>? maskSelectors = null,
        string? maskColor = null) {
        if (delayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be zero or positive.");
        }
        if (!string.IsNullOrEmpty(selector)) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForSelectorAsync(selector!, new PageWaitForSelectorOptions { Timeout = 10000 }).ConfigureAwait(false);
        }
        if (delayMs > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForTimeoutAsync(delayMs).ConfigureAwait(false);
        }

        var options = new PagePdfOptions {
            Landscape = landscape,
            PrintBackground = printBackground,
            Format = format switch {
                PdfPageFormat.A0 => "A0",
                PdfPageFormat.A1 => "A1",
                PdfPageFormat.A2 => "A2",
                PdfPageFormat.A3 => "A3",
                PdfPageFormat.A4 => "A4",
                PdfPageFormat.A5 => "A5",
                PdfPageFormat.A6 => "A6",
                PdfPageFormat.Letter => "Letter",
                PdfPageFormat.Legal => "Legal",
                PdfPageFormat.Tabloid => "Tabloid",
                PdfPageFormat.Ledger => "Ledger",
                _ => null
            },
            Width = width,
            Height = height,
            PageRanges = pageRanges,
            Scale = scale,
            DisplayHeaderFooter = displayHeaderFooter,
            HeaderTemplate = headerTemplate,
            FooterTemplate = footerTemplate,
            PreferCSSPageSize = preferCssPageSize,
            Outline = outline,
            Tagged = tagged
        };
        if (marginTop != null || marginRight != null || marginBottom != null || marginLeft != null) {
            options.Margin = new Margin {
                Top = marginTop,
                Right = marginRight,
                Bottom = marginBottom,
                Left = marginLeft
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await ExecuteWithTemporaryVisualMaskAsync(
            page,
            maskSensitiveElements,
            maskSelectors,
            maskColor,
            () => ExecutePdfOperationWithCancellationAsync(page, () => page.PdfAsync(options), cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports a PDF of the specified page URL and returns the bytes.
    /// </summary>
    public static async Task<byte[]> GetPagePdfAsync(
        string url,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        int delayMs = 0,
        string? selector = null,
        bool landscape = false,
        bool printBackground = false,
        PdfPageFormat? format = null,
        string? width = null,
        string? height = null,
        string? marginTop = null,
        string? marginRight = null,
        string? marginBottom = null,
        string? marginLeft = null,
        string? pageRanges = null,
        float? scale = null,
        bool displayHeaderFooter = false,
        string? headerTemplate = null,
        string? footerTemplate = null,
        bool preferCssPageSize = false,
        bool outline = false,
        bool tagged = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        CancellationToken cancellationToken = default,
        bool maskSensitiveElements = false,
        IEnumerable<string>? maskSelectors = null,
        string? maskColor = null) {
        EnsurePdfBrowserSupported(browser);
        if (delayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be zero or positive.");
        }
        await using HtmlBrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            null,
            proxy: proxy,
            proxyUsername: proxyUsername,
            proxyPassword: proxyPassword,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await GetPagePdfAsync(
            session.Page,
            delayMs,
            selector,
            landscape,
            printBackground,
            format,
            width,
            height,
            marginTop,
            marginRight,
            marginBottom,
            marginLeft,
            pageRanges,
            scale,
            displayHeaderFooter,
            headerTemplate,
            footerTemplate,
            preferCssPageSize,
            outline,
            tagged,
            cancellationToken,
            maskSensitiveElements,
            maskSelectors,
            maskColor).ConfigureAwait(false);
    }

    private static void EnsurePdfBrowserSupported(HtmlBrowserEngine browser) {
        if (browser != HtmlBrowserEngine.Chromium) {
            throw new NotSupportedException("Playwright PDF capture is supported only by Chromium. Firefox and WebKit cannot service a PDF request.");
        }
    }

    private static async Task<T> ExecutePdfOperationWithCancellationAsync<T>(IPage page, Func<Task<T>> operation, CancellationToken cancellationToken) {
        Task<T> task = operation();
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) {
            return await task.ConfigureAwait(false);
        }

        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) {
            try {
                if (!page.IsClosed) await page.CloseAsync().ConfigureAwait(false);
            } catch (PlaywrightException) {
                // Closing the page is a best-effort transport abort.
            }
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return await task.ConfigureAwait(false);
    }

}
