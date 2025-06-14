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
    /// Captures a screenshot of the specified page.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path for the screenshot.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Force re-download of browser runtimes.</param>
    /// <param name="fullPage">Capture the entire document instead of just the viewport.</param>
    /// <param name="delayMs">Additional wait time in milliseconds after the page is loaded.</param>
    /// <param name="selector">Optional CSS selector to wait for before capturing.</param>
    /// <param name="clipX">Optional clip region X coordinate.</param>
    /// <param name="clipY">Optional clip region Y coordinate.</param>
    /// <param name="clipWidth">Optional clip region width.</param>
    /// <param name="clipHeight">Optional clip region height.</param>
    public static async Task CaptureScreenshotAsync(
        string url,
        string path,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        bool fullPage = false,
        int delayMs = 0,
        string? selector = null,
        int? clipX = null,
        int? clipY = null,
        int? clipWidth = null,
        int? clipHeight = null,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null) {
        await using HtmlBrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            videoPath: null,
            proxy: proxy,
            proxyUsername: proxyUsername,
            proxyPassword: proxyPassword).ConfigureAwait(false);

        string fullPath = HtmlUtilities.ResolvePath(path);
        await CaptureScreenshotAsync(
            session.Page,
            fullPath,
            fullPage,
            delayMs,
            selector,
            clipX,
            clipY,
            clipWidth,
            clipHeight).ConfigureAwait(false);
    }

    /// <summary>
    /// Captures a screenshot of an already loaded page.
    /// </summary>
    public static async Task CaptureScreenshotAsync(
        IPage page,
        string path,
        bool fullPage = false,
        int delayMs = 0,
        string? selector = null,
        int? clipX = null,
        int? clipY = null,
        int? clipWidth = null,
        int? clipHeight = null) {
        if (!string.IsNullOrEmpty(selector)) {
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        if (delayMs > 0) {
            await page.WaitForTimeoutAsync(delayMs);
        }

        string fullPath = HtmlUtilities.ResolvePath(path);
        var options = new PageScreenshotOptions { Path = fullPath, FullPage = fullPage };
        if (clipX.HasValue && clipY.HasValue && clipWidth.HasValue && clipHeight.HasValue) {
            options.Clip = new Clip {
                X = clipX.Value,
                Y = clipY.Value,
                Width = clipWidth.Value,
                Height = clipHeight.Value,
            };
        }

        await page.ScreenshotAsync(options);
    }
}