using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

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
    /// <param name="headless">Run browser in headless mode.</param>
    /// <param name="slowMo">Slow motion delay in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paths of downloaded files.</returns>
    public static async IAsyncEnumerable<string> SavePageDownloadsAsync(string url, string directory, HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium, bool clean = false, string? filter = null, bool headless = true, int slowMo = 0, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
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
        string dir = directory.ToFullPath();
        await foreach (string file in SavePageDownloadsAsync(page, dir, filter, cancellationToken).ConfigureAwait(false)) {
            yield return file;
        }
    }

    /// <summary>
    /// Saves files downloaded from an already loaded page.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="directory">Directory where downloads should be saved.</param>
    /// <param name="filter">Optional substring filter applied to download URLs or file names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<string> SavePageDownloadsAsync(IPage page, string directory, string? filter = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {

        string dir = HtmlUtilities.EnsureDirectoryExists(directory);
        HashSet<string> downloads = new();
        List<Task> saveTasks = new();
        object sync = new();
        System.Threading.Channels.Channel<string> channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        void Handler(object? _, IDownload dl) {
            try {
                bool match = string.IsNullOrEmpty(filter) ||
                             dl.Url.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             dl.SuggestedFilename.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!match) {
                    return;
                }

                string filePath = Path.Combine(dir, dl.SuggestedFilename);
                bool save;
                lock (sync) {
                    save = downloads.Add(filePath);
                }
                if (!save) {
                    return;
                }

                Task saveTask = Task.Run(async () => {
                    try {
                        await dl.SaveAsAsync(filePath).ConfigureAwait(false);
                        await channel.Writer.WriteAsync(filePath, cancellationToken).ConfigureAwait(false);
                    } catch (Exception ex) {
                        channel.Writer.TryComplete(ex);
                    }
                }, cancellationToken);

                lock (sync) {
                    saveTasks.Add(saveTask);
                }
            } catch (Exception ex) {
                channel.Writer.TryComplete(ex);
            }
        }

        page.Download += Handler;

        Task producer = Task.Run(async () => {
            cancellationToken.ThrowIfCancellationRequested();
            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)").ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

            string selector = string.IsNullOrEmpty(filter)
                ? "a[download],a[href*='/download/'],a[href*='/archive/']"
                : $"a[href*=\"{filter}\"]";

            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 }).ConfigureAwait(false);
            var anchors = await page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
            foreach (var anchor in anchors) {
                cancellationToken.ThrowIfCancellationRequested();
                await page.RunAndWaitForDownloadAsync(() => anchor.ClickAsync()).ConfigureAwait(false);
            }

            await Task.WhenAll(saveTasks).ConfigureAwait(false);
            channel.Writer.Complete();
        }, cancellationToken);

        try {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                while (channel.Reader.TryRead(out string? path)) {
                    yield return path;
                }
            }
        } finally {
            page.Download -= Handler;
            await producer.ConfigureAwait(false);
        }
    }
}