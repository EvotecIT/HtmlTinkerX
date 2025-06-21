using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Playwright;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Saves any files downloaded while loading the specified URL.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="directory">Directory where downloads should be saved.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Reinstall the browser runtime.</param>
    /// <param name="filter">Optional substring filter applied to download URLs or file names.</param>
    /// <returns>Paths of downloaded files.</returns>
    public static async Task<List<string>> SavePageDownloadsAsync(string url, string directory, HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium, bool clean = false, string? filter = null, bool headless = true, int slowMo = 0, CancellationToken cancellationToken = default) {
        await using HtmlBrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            null,
            null,
            null,
            headless,
            slowMo,
            null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var page = session.Page;
        string dir = HtmlUtilities.ResolvePath(directory);
        return await SavePageDownloadsAsync(page, dir, filter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves files downloaded from an already loaded page.
    /// </summary>
    public static async Task<List<string>> SavePageDownloadsAsync(IPage page, string directory, string? filter = null, CancellationToken cancellationToken = default) {

        string dir = HtmlUtilities.ResolvePath(directory);
        Directory.CreateDirectory(dir);
        List<string> downloads = new();
        List<Task> saves = new();
        object sync = new();
        page.Download += (_, dl) => {
            bool match = string.IsNullOrEmpty(filter) ||
                         dl.Url.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         dl.SuggestedFilename.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!match) {
                return;
            }
            string filePath = Path.Combine(dir, dl.SuggestedFilename);
            bool save;
            lock (sync) {
                save = !downloads.Contains(filePath);
                if (save) {
                    downloads.Add(filePath);
                    saves.Add(dl.SaveAsAsync(filePath));
                }
            }
        };

        cancellationToken.ThrowIfCancellationRequested();
        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        string selector = string.IsNullOrEmpty(filter)
            ? "a[download],a[href*='/download/'],a[href*='/archive/']"
            : $"a[href*=\"{filter}\"]";

        cancellationToken.ThrowIfCancellationRequested();
        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
        var anchors = await page.QuerySelectorAllAsync(selector);
        foreach (var anchor in anchors) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.RunAndWaitForDownloadAsync(() => anchor.ClickAsync());
        }
        await Task.WhenAll(saves).ConfigureAwait(false);
        return downloads;
    }
}