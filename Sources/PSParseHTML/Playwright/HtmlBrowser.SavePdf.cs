using System;
using System.IO;
using System.Runtime.InteropServices;
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
            int slowMo = 0) {
        await using HtmlBrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            null).ConfigureAwait(false);

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
            tagged).ConfigureAwait(false);
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
        bool tagged = false) {
        if (!string.IsNullOrEmpty(selector)) {
            await page.WaitForSelectorAsync(selector!, new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        if (delayMs > 0) {
            await page.WaitForTimeoutAsync(delayMs);
        }

        var options = new PagePdfOptions {
            Path = path,
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

        await page.PdfAsync(options).ConfigureAwait(false);
    }
}