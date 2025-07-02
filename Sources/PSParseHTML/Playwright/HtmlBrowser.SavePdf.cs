using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PSParseHTML;

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
            string? format = null,
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
        CancellationToken cancellationToken = default) {
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
            cancellationToken).ConfigureAwait(false);
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
        string? format = null,
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
        CancellationToken cancellationToken = default) {
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

        string fullPath = HtmlUtilities.ResolvePath(path);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
        var options = new PagePdfOptions {
            Path = fullPath,
            Landscape = landscape,
            PrintBackground = printBackground,
            Format = format,
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
        await page.PdfAsync(options).ConfigureAwait(false);
    }
}