using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

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
    /// <param name="elementSelector">CSS selector of an element to capture.</param>
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
        string? elementSelector = null,
        int? clipX = null,
        int? clipY = null,
        int? clipWidth = null,
        int? clipHeight = null,
        IEnumerable<string>? highlightSelectors = null,
        string? overlayText = null,
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
            null,
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
            elementSelector,
            clipX,
            clipY,
            clipWidth,
            clipHeight,
            highlightSelectors,
            overlayText).ConfigureAwait(false);
    }

    /// <summary>
    /// Captures a screenshot of an already loaded page.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="path">File path for the screenshot.</param>
    /// <param name="fullPage">Capture the entire document instead of just the viewport.</param>
    /// <param name="delayMs">Additional wait time in milliseconds after the page is loaded.</param>
    /// <param name="selector">Optional CSS selector to wait for before capturing.</param>
    /// <param name="elementSelector">CSS selector of an element to capture.</param>
    /// <param name="clipX">Optional clip region X coordinate.</param>
    /// <param name="clipY">Optional clip region Y coordinate.</param>
    /// <param name="clipWidth">Optional clip region width.</param>
    /// <param name="clipHeight">Optional clip region height.</param>
    public static async Task CaptureScreenshotAsync(
        IPage page,
        string path,
        bool fullPage = false,
        int delayMs = 0,
        string? selector = null,
        string? elementSelector = null,
        int? clipX = null,
        int? clipY = null,
        int? clipWidth = null,
        int? clipHeight = null,
        IEnumerable<string>? highlightSelectors = null,
        string? overlayText = null) {
        if (!string.IsNullOrEmpty(selector)) {
            await page.WaitForSelectorAsync(selector!, new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        if (delayMs > 0) {
            await page.WaitForTimeoutAsync(delayMs);
        }

        if (!string.IsNullOrEmpty(elementSelector)) {
            var locator = page.Locator(elementSelector!);
            var box = await locator.BoundingBoxAsync();
            if (box != null) {
                clipX = (int)Math.Floor(box.X);
                clipY = (int)Math.Floor(box.Y);
                clipWidth = (int)Math.Ceiling(box.Width);
                clipHeight = (int)Math.Ceiling(box.Height);
            }
        }

        string fullPath = HtmlUtilities.ResolvePath(path);
        var options = new PageScreenshotOptions { FullPage = fullPage };
        if (clipX.HasValue && clipY.HasValue && clipWidth.HasValue && clipHeight.HasValue) {
            options.Clip = new Clip {
                X = clipX.Value,
                Y = clipY.Value,
                Width = clipWidth.Value,
                Height = clipHeight.Value,
            };
        }
        byte[] data = await page.ScreenshotAsync(options);

        if ((highlightSelectors != null && System.Linq.Enumerable.Any(highlightSelectors)) || !string.IsNullOrEmpty(overlayText)) {
            using var image = SixLabors.ImageSharp.Image.Load(data);
            var pen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(SixLabors.ImageSharp.Color.Red, 3);
            if (highlightSelectors != null) {
                foreach (string sel in highlightSelectors) {
                    try {
                        var elements = await page.QuerySelectorAllAsync(sel);
                        foreach (var element in elements) {
                            var box = await element.BoundingBoxAsync();
                            if (box != null) {
                                var rect = new SixLabors.ImageSharp.RectangleF((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height);
                                image.Mutate(c => c.Draw(pen, rect));
                            }
                        }
                    } catch {
                        // ignore selector failures
                    }
                }
            }

            if (!string.IsNullOrEmpty(overlayText)) {
                SixLabors.Fonts.FontFamily fontFamily;
                if (!SixLabors.Fonts.SystemFonts.TryGet("DejaVu Sans", out fontFamily) &&
                    !SixLabors.Fonts.SystemFonts.TryGet("Arial", out fontFamily)) {
                    fontFamily = SixLabors.Fonts.SystemFonts.Collection.Families.First();
                }
                var font = fontFamily.CreateFont(20);
                image.Mutate(c => c.DrawText(overlayText, font, SixLabors.ImageSharp.Color.Red, new SixLabors.ImageSharp.PointF(10, 10)));
            }

            await image.SaveAsync(fullPath);
        } else {
            File.WriteAllBytes(fullPath, data);
        }
    }
}