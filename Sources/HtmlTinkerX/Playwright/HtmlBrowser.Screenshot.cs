using Microsoft.Playwright;
using ChartForgeX.Composition;
using ChartForgeX.Core;
using ChartForgeX.Primitives;
using ChartForgeX.Raster;
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

        string fullPath = path.ToFullPath();
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
            var composition = ImageComposition.FromBytes(data);
            if (options.HighlightSelectors != null) {
                foreach (string sel in options.HighlightSelectors) {
                    try {
                        var elements = await page.QuerySelectorAllAsync(sel);
                        foreach (var element in elements) {
                            var box = await element.BoundingBoxAsync();
                            if (box != null && box.Width > 0 && box.Height > 0) {
                                composition.StrokeRectangle(box.X, box.Y, box.Width, box.Height, ChartColors.Red, 3);
                            }
                        }
                    } catch {
                        // ignore selector failures
                    }
                }
            }

            string? overlayText = options.OverlayText;
            if (overlayText != null && overlayText.Length > 0) {
                composition.DrawText(10, 10, Math.Max(1, composition.Width - 20), overlayText, 20, ChartColors.Red);
            }
            File.WriteAllBytes(fullPath, composition.ToRasterImage(ToRasterImageFormat(options.Format), GetRasterImageOptions(options.Quality)));
        } else {
            File.WriteAllBytes(fullPath, data);
        }
    }

    private static RasterImageFormat ToRasterImageFormat(ImageFormat format) => format switch {
        ImageFormat.Jpeg => RasterImageFormat.Jpeg,
        ImageFormat.Bmp => RasterImageFormat.Bmp,
        ImageFormat.Gif => RasterImageFormat.Gif,
        _ => RasterImageFormat.Png
    };

    private static RasterImageOptions GetRasterImageOptions(int quality) => new() {
        Background = ChartColors.White,
        JpegQuality = ClampJpegQuality(quality),
        PngCompressionLevel = QualityToCompression(quality)
    };

    private static int ClampJpegQuality(int quality) {
        if (quality < 1) {
            return 1;
        }
        if (quality > 100) {
            return 100;
        }
        return quality;
    }

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
