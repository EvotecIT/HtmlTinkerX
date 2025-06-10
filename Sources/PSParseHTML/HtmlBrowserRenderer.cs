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

    private static async Task<BrowserSession> CreatePageAsync(
        string url,
        BrowserEngine browser,
        bool clean,
        string? username,
        string? password,
        FormLoginOptions? formLogin) {
        if (clean) {
            CleanInstallDir();
        }

        string engine = browser.ToString().ToLowerInvariant();
        Microsoft.Playwright.Program.Main(new[] { "install", engine });

        var playwright = await Playwright.CreateAsync();
        IBrowserType type = browser switch {
            BrowserEngine.Firefox => playwright.Firefox,
            BrowserEngine.Webkit => playwright.Webkit,
            _ => playwright.Chromium,
        };

        var browserInstance = await type.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        BrowserNewContextOptions? contextOptions = null;
        if (formLogin == null && !string.IsNullOrEmpty(username) && password != null) {
            contextOptions = new BrowserNewContextOptions {
                HttpCredentials = new HttpCredentials {
                    Username = username,
                    Password = password
                }
            };
        }
        contextOptions ??= new BrowserNewContextOptions();
        contextOptions.IgnoreHTTPSErrors = true;

        var context = await browserInstance.NewContextAsync(contextOptions);
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

        return new BrowserSession(playwright, browserInstance, context, page);
    }

    /// <summary>
    /// Creates a new <see cref="BrowserSession"/> and navigates to the specified URL.
    /// </summary>
    public static Task<BrowserSession> OpenSessionAsync(
        string url,
        BrowserEngine browser = BrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        FormLoginOptions? formLogin = null)
        => CreatePageAsync(url, browser, clean, username, password, formLogin);

    /// <summary>
    /// Disposes the specified browser session.
    /// </summary>
    public static async Task CloseSessionAsync(BrowserSession session) {
        if (session != null) {
            await session.DisposeAsync().ConfigureAwait(false);
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
        await using BrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin).ConfigureAwait(false);

        return await session.Page.ContentAsync().ConfigureAwait(false);
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
        await using BrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin).ConfigureAwait(false);

        await CaptureScreenshotAsync(
            session.Page,
            path,
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
        await using BrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            null,
            null,
            null).ConfigureAwait(false);
        var page = session.Page;
        return await SavePageDownloadsAsync(page, directory, filter).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves files downloaded from an already loaded page.
    /// </summary>
    public static async Task<List<string>> SavePageDownloadsAsync(
        IPage page,
        string directory,
        string? filter = null) {

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

    /// <summary>
    /// Gets HTML content from an already loaded page or element.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="selector">Optional CSS selector for the element.</param>
    /// <param name="innerHtml">Return inner HTML instead of outer HTML.</param>
    /// <param name="asText">Return text content instead of markup.</param>
    /// <returns>Extracted markup or text.</returns>
    public static async Task<string> GetContentAsync(
        IPage page,
        string? selector = null,
        bool innerHtml = false,
        bool asText = false) {
        if (string.IsNullOrEmpty(selector)) {
            if (asText) {
                return await page.InnerTextAsync("html").ConfigureAwait(false);
            }
            return await page.ContentAsync().ConfigureAwait(false);
        }

        var locator = page.Locator(selector);
        await locator.WaitForAsync();

        if (asText) {
            return await locator.InnerTextAsync().ConfigureAwait(false);
        }
        if (innerHtml) {
            return await locator.InnerHTMLAsync().ConfigureAwait(false);
        }
        return await locator.EvaluateAsync<string>("el => el.outerHTML").ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a list of elements that can be interacted with (links, buttons, etc.).
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <returns>List of interactable element descriptions.</returns>
    public static async Task<List<HtmlInteractableInfo>> GetInteractablesAsync(IPage page) {
        var elements = await page.QuerySelectorAllAsync("a,button,[role=button],input[type=button],input[type=submit]");
        List<HtmlInteractableInfo> list = new();
        int index = 0;
        foreach (var el in elements) {
            string rawText = await el.InnerTextAsync();
            string text = Regex.Replace(rawText, "\\s+", " ").Trim();
            string tag = await el.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
            string? href = await el.GetAttributeAsync("href");
            string? id = await el.GetAttributeAsync("id");
            string? cls = await el.GetAttributeAsync("class");
            bool visible = await el.IsVisibleAsync();
            string selector = await el.EvaluateAsync<string>(@"el => {
                const esc = (CSS && CSS.escape) ? CSS.escape : (s => s);
                let sel = el.tagName.toLowerCase();
                if (el.id) return sel + '#' + esc(el.id);
                const href = el.getAttribute('href');
                if (href) return sel + '[href=""' + esc(href) + '""]';
                const cls = el.className;
                if (cls) return sel + '.' + cls.trim().split(/\s+/).map(esc).join('.');
                return sel;
            }");
            list.Add(new HtmlInteractableInfo {
                Index = index++,
                Text = text,
                Tag = tag,
                Selector = selector,
                Href = href,
                Id = id,
                Class = cls,
                Visible = visible
            });
        }
        return list;
    }

    /// <summary>
    /// Navigates the specified session to a new URL and waits for the network to be idle.
    /// </summary>
    public static async Task NavigateAsync(BrowserSession session, string url, int timeout = 30000) {
        await session.Page.GotoAsync(url, new PageGotoOptions { Timeout = timeout }).ConfigureAwait(false);
        await session.Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = timeout }).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element by CSS selector.
    /// </summary>
    public static async Task ClickSelectorAsync(BrowserSession session, string selector, bool waitForNavigation = false, int timeout = 30000) {
        if (waitForNavigation) {
            await session.Page.RunAndWaitForNavigationAsync(
                () => session.Page.ClickAsync(selector, new PageClickOptions { Timeout = timeout }),
                new PageRunAndWaitForNavigationOptions { Timeout = timeout }).ConfigureAwait(false);
        } else {
            await session.Page.ClickAsync(selector, new PageClickOptions { Timeout = timeout }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clicks an element specified by text content.
    /// </summary>
    public static async Task ClickTextAsync(
        BrowserSession session,
        string text,
        bool exact = false,
        string? regex = null,
        bool waitForNavigation = false,
        int timeout = 30000) {
        ILocator locator = !string.IsNullOrEmpty(regex)
            ? session.Page.GetByText(new Regex(regex))
            : exact
                ? session.Page.GetByText(text, new PageGetByTextOptions { Exact = true })
                : session.Page.GetByText(text);

        if (waitForNavigation) {
            await session.Page.RunAndWaitForNavigationAsync(
                () => locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }),
                new PageRunAndWaitForNavigationOptions { Timeout = timeout }).ConfigureAwait(false);
        } else {
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Extracts a concise message from a Playwright strict mode violation error.
    /// </summary>
    public static string FormatStrictModeMessage(string query, PlaywrightException ex) {
        string text = ex.Message;
        int start = text.IndexOf("strict mode violation:", StringComparison.Ordinal);
        if (start >= 0) {
            text = text.Substring(start + "strict mode violation:".Length).Trim();
        }
        int idx = text.IndexOf("Call log:", StringComparison.Ordinal);
        if (idx > 0) {
            text = text.Substring(0, idx).TrimEnd();
        }
        string[] parts = text.Replace("  ", " ").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return $"Strict mode violation for '{query}':" + Environment.NewLine + string.Join(Environment.NewLine, parts);
    }
}
