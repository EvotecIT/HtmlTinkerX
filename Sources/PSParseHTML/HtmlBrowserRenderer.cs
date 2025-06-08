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
    /// <returns>The rendered HTML markup.</returns>
    public static async Task<string> GetPageContentAsync(
        string url,
        BrowserEngine browser = BrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        FormLoginOptions? formLogin = null) {
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
        BrowserNewContextOptions? contextOptions = null;
        if (formLogin == null && !string.IsNullOrEmpty(username) && password != null) {
            contextOptions = new BrowserNewContextOptions {
                HttpCredentials = new HttpCredentials {
                    Username = username,
                    Password = password
                }
            };
        }
        await using var context = await browserInstance.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();
        if (formLogin != null) {
            await page.GotoAsync(formLogin.LoginUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if (username != null) {
                await page.FillAsync(formLogin.UsernameSelector, username);
            }
            if (password != null) {
                await page.FillAsync(formLogin.PasswordSelector, password);
            }
            await page.ClickAsync(formLogin.SubmitSelector);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return await page.ContentAsync();
    }

    /// <summary>
    /// Saves the rendered HTML to a file.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path to write.</param>
    public static async Task SavePageContentAsync(
        string url,
        string path,
        BrowserEngine browser = BrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        FormLoginOptions? formLogin = null) {
        string content = await GetPageContentAsync(url, browser, clean, username, password, formLogin).ConfigureAwait(false);
        File.WriteAllText(path, content);
    }

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
    BrowserEngine browser = BrowserEngine.Chromium,
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
    FormLoginOptions? formLogin = null) {
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
        BrowserNewContextOptions? contextOptions = null;
        if (formLogin == null && !string.IsNullOrEmpty(username) && password != null) {
            contextOptions = new BrowserNewContextOptions {
                HttpCredentials = new HttpCredentials {
                    Username = username,
                    Password = password
                }
            };
        }
        await using var context = await browserInstance.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();
        if (formLogin != null) {
            await page.GotoAsync(formLogin.LoginUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if (username != null) {
                await page.FillAsync(formLogin.UsernameSelector, username);
            }
            if (password != null) {
                await page.FillAsync(formLogin.PasswordSelector, password);
            }
            await page.ClickAsync(formLogin.SubmitSelector);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        if (!string.IsNullOrEmpty(selector)) {
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        if (delayMs > 0) {
            await page.WaitForTimeoutAsync(delayMs);
        }

        var options = new PageScreenshotOptions { Path = path, FullPage = fullPage };
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

    /// <summary>
    /// Saves any files downloaded while loading the specified URL.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="directory">Directory where downloads should be saved.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Reinstall the browser runtime.</param>
    /// <param name="filter">Optional substring filter applied to download URLs or file names.</param>
    /// <returns>Paths of downloaded files.</returns>
    public static async Task<List<string>> SavePageDownloadsAsync(
        string url,
        string directory,
        BrowserEngine browser = BrowserEngine.Chromium,
        bool clean = false,
        string? filter = null) {
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

        Directory.CreateDirectory(directory);
        List<string> downloads = new();
        page.Download += async (_, dl) => {
            bool match = string.IsNullOrEmpty(filter) ||
                         dl.Url.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         dl.SuggestedFilename.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            if (match) {
                string filePath = Path.Combine(directory, dl.SuggestedFilename);
                await dl.SaveAsAsync(filePath);
                downloads.Add(filePath);
            }
        };

        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        string selector = string.IsNullOrEmpty(filter)
            ? "a[download],a[href*='/download/'],a[href*='/archive/']"
            : $"a[href*=\"{filter}\"]";

        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
        var anchors = await page.QuerySelectorAllAsync(selector);
        foreach (var anchor in anchors) {
            var download = await page.RunAndWaitForDownloadAsync(() => anchor.ClickAsync());
            string filePath = Path.Combine(directory, download.SuggestedFilename);
            await download.SaveAsAsync(filePath);
            if (!downloads.Contains(filePath)) {
                downloads.Add(filePath);
            }
        }

        await page.WaitForTimeoutAsync(500);
        return downloads;
    }
}
