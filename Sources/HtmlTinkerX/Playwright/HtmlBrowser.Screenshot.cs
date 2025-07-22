using Microsoft.Playwright;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

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
    /// <param name="options">Screenshot capture options.</param>
    /// <param name="username">Username for authentication.</param>
    /// <param name="password">Password for authentication.</param>
    /// <param name="formLogin">Form based login parameters.</param>
    /// <param name="headless">Run browser in headless mode.</param>
    /// <param name="slowMo">Slow motion delay in milliseconds.</param>
    /// <param name="proxy">Proxy server URL.</param>
    /// <param name="proxyUsername">Proxy username.</param>
    /// <param name="proxyPassword">Proxy password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task CaptureScreenshotAsync(
        string url,
        string path,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        ScreenshotOptions? options = null,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        CancellationToken cancellationToken = default) {
        options ??= new ScreenshotOptions();
        if (options.DelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.DelayMs), "Delay must be zero or positive.");
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

        string fullPath = HtmlUtilities.ResolvePath(path);
        await CaptureScreenshotAsync(
            session.Page,
            fullPath,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Captures a screenshot of an already loaded page.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="path">File path for the screenshot.</param>
    /// <param name="options">Screenshot capture options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task CaptureScreenshotAsync(
        IPage page,
        string path,
        ScreenshotOptions? options = null,
        CancellationToken cancellationToken = default) {
        options ??= new ScreenshotOptions();
        if (options.DelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.DelayMs), "Delay must be zero or positive.");
        }
        if (!string.IsNullOrEmpty(options.Selector)) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForSelectorAsync(options.Selector!, new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        if (options.DelayMs > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForTimeoutAsync(options.DelayMs);
        }

        if (!string.IsNullOrEmpty(options.ElementSelector)) {
            var locator = page.Locator(options.ElementSelector!);
            var box = await locator.BoundingBoxAsync();
            if (box != null) {
                options.ClipX = (int)Math.Floor(box.X);
                options.ClipY = (int)Math.Floor(box.Y);
                options.ClipWidth = (int)Math.Ceiling(box.Width);
                options.ClipHeight = (int)Math.Ceiling(box.Height);
            }
        }

        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
        var pwOptions = new PageScreenshotOptions { FullPage = options.FullPage };
        pwOptions.Type = options.Format == ImageFormat.Jpeg ? ScreenshotType.Jpeg : ScreenshotType.Png;
        if (pwOptions.Type == ScreenshotType.Jpeg) {
            pwOptions.Quality = options.Quality;
        }
        if (options.ClipX.HasValue && options.ClipY.HasValue && options.ClipWidth.HasValue && options.ClipHeight.HasValue) {
            pwOptions.Clip = new Clip {
                X = options.ClipX.Value,
                Y = options.ClipY.Value,
                Width = options.ClipWidth.Value,
                Height = options.ClipHeight.Value,
            };
        }
        cancellationToken.ThrowIfCancellationRequested();
        byte[] data = await page.ScreenshotAsync(pwOptions);

        bool needsProcessing = (options.HighlightSelectors != null && System.Linq.Enumerable.Any(options.HighlightSelectors)) || !string.IsNullOrEmpty(options.OverlayText) || (options.Format != ImageFormat.Png && options.Format != ImageFormat.Jpeg);
        if (needsProcessing) {
            using var image = SixLabors.ImageSharp.Image.Load(data);
            var pen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(SixLabors.ImageSharp.Color.Red, 3);
            if (options.HighlightSelectors != null) {
                foreach (string sel in options.HighlightSelectors) {
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

            if (!string.IsNullOrEmpty(options.OverlayText)) {
                SixLabors.Fonts.FontFamily fontFamily;
                if (!SixLabors.Fonts.SystemFonts.TryGet("DejaVu Sans", out fontFamily) &&
                    !SixLabors.Fonts.SystemFonts.TryGet("Arial", out fontFamily)) {
                    fontFamily = SixLabors.Fonts.SystemFonts.Collection.Families.First();
                }
                var font = fontFamily.CreateFont(20);
                image.Mutate(c => c.DrawText(options.OverlayText, font, SixLabors.ImageSharp.Color.Red, new SixLabors.ImageSharp.PointF(10, 10)));
            }
            await image.SaveAsync(fullPath, GetEncoder(options.Format, options.Quality));
        } else {
            File.WriteAllBytes(fullPath, data);
        }
    }

    private static IImageEncoder GetEncoder(ImageFormat format, int quality) => format switch {
        ImageFormat.Jpeg => new JpegEncoder { Quality = quality },
        ImageFormat.Bmp => new BmpEncoder(),
        ImageFormat.Gif => new GifEncoder(),
        _ => new PngEncoder { CompressionLevel = (PngCompressionLevel)QualityToCompression(quality) }
    };

    private static int QualityToCompression(int quality) {
        if (quality < 0) {
            quality = 0;
        } else if (quality > 100) {
            quality = 100;
        }
        int level = (int)Math.Round((100 - quality) / 10.0);
        if (level < 0) {
            level = 0;
        } else if (level > 9) {
            level = 9;
        }
        return level;
    }
}