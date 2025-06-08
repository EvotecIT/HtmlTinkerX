using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Playwright;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public enum BrowserEngine {
    Chromium,
    Firefox,
    Webkit,
}

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static class HtmlBrowserRenderer {
    private static string GetBrowserInstallPath() {
        string? envDefined = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (envDefined == "0") {
            return Path.Combine(Path.GetDirectoryName(typeof(Playwright).Assembly.Location) ?? AppContext.BaseDirectory, ".local-browsers");
        }
        if (!string.IsNullOrEmpty(envDefined)) {
            return Path.GetFullPath(envDefined);
        }
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ms-playwright");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return Path.Combine(user, "Library", "Caches", "ms-playwright");
        }
        return Path.Combine(user, ".cache", "ms-playwright");
    }

    private static void CleanInstallDir() {
        string path = GetBrowserInstallPath();
        if (Directory.Exists(path)) {
            Directory.Delete(path, recursive: true);
        }
    }
    /// <summary>
    /// Retrieves the fully rendered HTML from the specified URL after executing JavaScript.
    /// </summary>
    /// <param name="url">The URL to load.</param>
    /// <returns>The rendered HTML markup and any downloaded files.</returns>
    public static async Task<HtmlRenderResult> GetPageContentAsync(
        string url,
        BrowserEngine browser = BrowserEngine.Chromium,
        bool clean = false,
        string? downloadPath = null,
        string? downloadFilter = null) {
        if (clean) {
            CleanInstallDir();
        }
        string engine = browser.ToString().ToLowerInvariant();
        Microsoft.Playwright.Program.Main(new[] { "install", engine });
        using var playwright = await Playwright.CreateAsync();
        IBrowserType type = browser switch {
            BrowserEngine.Firefox => playwright.Firefox,
            BrowserEngine.Webkit => playwright.Webkit,
            _ => playwright.Chromium,
        };
        await using var browserInstance = await type.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browserInstance.NewPageAsync();
        List<string> downloads = new();
        if (!string.IsNullOrEmpty(downloadPath)) {
            Directory.CreateDirectory(downloadPath);
            page.Download += async (_, dl) => {
                bool match = string.IsNullOrEmpty(downloadFilter);
                if (!match) {
                    match = dl.Url.IndexOf(downloadFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            dl.SuggestedFilename.IndexOf(downloadFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (match) {
                    string filePath = Path.Combine(downloadPath, dl.SuggestedFilename);
                    await dl.SaveAsAsync(filePath);
                    downloads.Add(filePath);
                }
            };
        }
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        if (!string.IsNullOrEmpty(downloadPath)) {
            await page.WaitForTimeoutAsync(500);
        }
        string html = await page.ContentAsync();
        return new HtmlRenderResult { Html = html, Downloads = downloads };
    }

    /// <summary>
    /// Saves the rendered HTML to a file.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path to write.</param>
    public static async Task SavePageContentAsync(string url, string path, BrowserEngine browser = BrowserEngine.Chromium, bool clean = false) {
        HtmlRenderResult result = await GetPageContentAsync(url, browser, clean).ConfigureAwait(false);
        File.WriteAllText(path, result.Html);
    }
}
