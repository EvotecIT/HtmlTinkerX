using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
    /// <returns>The rendered HTML markup.</returns>
    public static async Task<string> GetPageContentAsync(string url, BrowserEngine browser = BrowserEngine.Chromium, bool clean = false) {
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
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return await page.ContentAsync();
    }

    /// <summary>
    /// Saves the rendered HTML to a file.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path to write.</param>
    public static async Task SavePageContentAsync(string url, string path, BrowserEngine browser = BrowserEngine.Chromium, bool clean = false) {
        string content = await GetPageContentAsync(url, browser, clean).ConfigureAwait(false);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Captures a screenshot of the specified page.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path for the screenshot.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Force re-download of browser runtimes.</param>
    public static async Task CaptureScreenshotAsync(string url, string path, BrowserEngine browser = BrowserEngine.Chromium, bool clean = false) {
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
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
    }
}
