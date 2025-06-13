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
public static partial class HtmlBrowserRenderer {
    /// <summary>
    /// Saves any files downloaded while loading the specified URL.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="directory">Directory where downloads should be saved.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Reinstall the browser runtime.</param>
    /// <param name="filter">Optional substring filter applied to download URLs or file names.</param>
    /// <returns>Paths of downloaded files.</returns>
    public static async Task<List<string>> SavePageDownloadsAsync(string url, string directory, BrowserEngine browser = BrowserEngine.Chromium, bool clean = false, string? filter = null, bool headless = true, int slowMo = 0) {
        await using BrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            null,
            null,
            null,
            headless,
            slowMo).ConfigureAwait(false);
        var page = session.Page;
        string dir = FileUtilities.ResolvePath(directory);
        return await SavePageDownloadsAsync(page, dir, filter).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves files downloaded from an already loaded page.
    /// </summary>
    public static async Task<List<string>> SavePageDownloadsAsync(IPage page, string directory, string? filter = null) {

        string dir = FileUtilities.ResolvePath(directory);
        Directory.CreateDirectory(dir);
        List<string> downloads = new();
        page.Download += async (_, dl) => {
            bool match = string.IsNullOrEmpty(filter) ||
                         dl.Url.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         dl.SuggestedFilename.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            if (match) {
                string filePath = Path.Combine(dir, dl.SuggestedFilename);
                await dl.SaveAsAsync(filePath);
                downloads.Add(filePath);
            }
        };

        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        string selector = string.IsNullOrEmpty(filter)
            ? "a[download],a[href*='/download/'],a[href*='/archive/']"
            : $"a[href*=\"{filter}\"]";

        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
        var anchors = await page.QuerySelectorAllAsync(selector);
        foreach (var anchor in anchors) {
            var download = await page.RunAndWaitForDownloadAsync(() => anchor.ClickAsync());
            string filePath = Path.Combine(dir, download.SuggestedFilename);
            await download.SaveAsAsync(filePath);
            if (!downloads.Contains(filePath)) {
                downloads.Add(filePath);
            }
        }

        await page.WaitForTimeoutAsync(500);
        return downloads;
    }
}