using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Creates a new Playwright browser session and navigates to the specified URL.
    /// </summary>
    private static async Task<HtmlBrowserSession> CreatePageAsync(
        string url,
        HtmlBrowserEngine browser,
        bool clean,
        string? username,
        string? password,
        HtmlFormLogin? formLogin,
        bool headless = true,
        int slowMo = 0,
        string? videoPath = null,
        int videoWidth = 800,
        int videoHeight = 600,
        string? storageStatePath = null,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null) {
        if (clean) {
            CleanInstallDir();
        }

        await EnsureInstalledAsync().ConfigureAwait(false);

        string engine = browser.ToString().ToLowerInvariant();
        Microsoft.Playwright.Program.Main(new[] { "install", engine });

        var playwright = await Playwright.CreateAsync();
        IBrowserType type = browser switch {
            HtmlBrowserEngine.Firefox => playwright.Firefox,
            HtmlBrowserEngine.Webkit => playwright.Webkit,
            _ => playwright.Chromium,
        };

        var launchOptions = new BrowserTypeLaunchOptions {
            Headless = headless,
            SlowMo = slowMo
        };
        if (!string.IsNullOrEmpty(proxy)) {
            launchOptions.Proxy = new Proxy {
                Server = proxy,
                Username = proxyUsername,
                Password = proxyPassword
            };
        }
        var browserInstance = await type.LaunchAsync(launchOptions);
        BrowserNewContextOptions? contextOptions = null;
        if (formLogin == null && !string.IsNullOrEmpty(username) && password != null) {
            contextOptions = new BrowserNewContextOptions {
                HttpCredentials = new HttpCredentials {
                    Username = username!,
                    Password = password!
                }
            };
        }
        contextOptions ??= new BrowserNewContextOptions();
        contextOptions.IgnoreHTTPSErrors = true;
        if (!string.IsNullOrEmpty(storageStatePath)) {
            contextOptions.StorageStatePath = storageStatePath;
        }
        if (!string.IsNullOrEmpty(videoPath)) {
            string resolved = HtmlUtilities.ResolvePath(videoPath!);
            string dir = Path.GetDirectoryName(resolved) ?? resolved;
            Directory.CreateDirectory(dir);
            contextOptions.RecordVideoDir = dir;
            contextOptions.RecordVideoSize = new RecordVideoSize { Width = videoWidth, Height = videoHeight };
        }
        if (!string.IsNullOrEmpty(userAgent)) {
            contextOptions.UserAgent = userAgent;
        }
        if (viewportWidth.HasValue && viewportHeight.HasValue) {
            contextOptions.ViewportSize = new ViewportSize { Width = viewportWidth.Value, Height = viewportHeight.Value };
        }
        if (deviceScaleFactor.HasValue) {
            contextOptions.DeviceScaleFactor = deviceScaleFactor.Value;
        }
        if (geoLatitude.HasValue && geoLongitude.HasValue) {
            contextOptions.Geolocation = new Geolocation {
                Latitude = (float)geoLatitude.Value,
                Longitude = (float)geoLongitude.Value,
                Accuracy = 0
            };
            contextOptions.Permissions = new[] { "geolocation" };
        }
        if (!string.IsNullOrEmpty(timezone)) {
            contextOptions.TimezoneId = timezone;
        }

        var context = await browserInstance.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();

        var network = new System.Collections.Concurrent.ConcurrentDictionary<IRequest, HtmlNetworkEntry>();
        page.Request += (_, req) => {
            HtmlNetworkEntry entry = new() {
                Url = req.Url,
                Method = req.Method,
                RequestHeaders = new System.Collections.Generic.Dictionary<string, string>(req.Headers)
            };
            network[req] = entry;
        };
        page.Response += (_, res) => {
            HtmlNetworkEntry entry = network.GetOrAdd(res.Request, r => new HtmlNetworkEntry {
                Url = r.Url,
                Method = r.Method,
                RequestHeaders = new System.Collections.Generic.Dictionary<string, string>(r.Headers)
            });
            entry.Status = res.Status;
            entry.ResponseHeaders = new System.Collections.Generic.Dictionary<string, string>(res.Headers);
        };

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

        IVideo? video = null;
        if (!string.IsNullOrEmpty(videoPath)) {
            video = page.Video;
        }

        return new HtmlBrowserSession(playwright, browserInstance, context, page, video, videoPath, network);
    }

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> and navigates to the specified URL.
    /// </summary>
    public static Task<HtmlBrowserSession> OpenSessionAsync(
        string url,
        HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium,
        bool clean = false,
        string? username = null,
        string? password = null,
        HtmlFormLogin? formLogin = null,
        bool headless = true,
        int slowMo = 0,
        string? videoPath = null,
        int videoWidth = 800,
        int videoHeight = 600,
        string? storageStatePath = null,
        string? userAgent = null,
        int? viewportWidth = null,
        int? viewportHeight = null,
        float? deviceScaleFactor = null,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        double? geoLatitude = null,
        double? geoLongitude = null,
        string? timezone = null)
        => CreatePageAsync(url, browser, clean, username, password, formLogin, headless, slowMo, videoPath, videoWidth, videoHeight, storageStatePath, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, proxy, proxyUsername, proxyPassword, geoLatitude, geoLongitude, timezone);

    /// <summary>
    /// Disposes the specified browser session.
    /// </summary>
    public static async Task CloseSessionAsync(HtmlBrowserSession session) {
        if (session != null) {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
    /// <summary>
    /// Retrieves the fully rendered HTML from the specified URL after executing JavaScript.
    /// </summary>
    /// <param name="url">The URL to load.</param>
    /// <returns>The rendered HTML markup.</returns>
    public static async Task<string> GetPageContentAsync(string url, HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium, bool clean = false, string? username = null, string? password = null, HtmlFormLogin? formLogin = null, bool headless = true, int slowMo = 0, string? userAgent = null, int? viewportWidth = null, int? viewportHeight = null, float? deviceScaleFactor = null, string? proxy = null, string? proxyUsername = null, string? proxyPassword = null, double? geoLatitude = null, double? geoLongitude = null, string? timezone = null) {
        await using HtmlBrowserSession session = await OpenSessionAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            videoPath: null,
            videoWidth: 800,
            videoHeight: 600,
            storageStatePath: null,
            userAgent: userAgent,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight,
            deviceScaleFactor: deviceScaleFactor,
            proxy: proxy,
            proxyUsername: proxyUsername,
            proxyPassword: proxyPassword,
            geoLatitude: geoLatitude,
            geoLongitude: geoLongitude,
            timezone: timezone).ConfigureAwait(false);

        return await session.Page.ContentAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the rendered HTML to a file.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path to write.</param>
    public static async Task SavePageContentAsync(string url, string path, HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium, bool clean = false, string? username = null, string? password = null, HtmlFormLogin? formLogin = null, bool headless = true, int slowMo = 0, string? userAgent = null, int? viewportWidth = null, int? viewportHeight = null, float? deviceScaleFactor = null, string? proxy = null, string? proxyUsername = null, string? proxyPassword = null, double? geoLatitude = null, double? geoLongitude = null, string? timezone = null) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        string content = await GetPageContentAsync(url, browser, clean, username, password, formLogin, headless, slowMo, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, proxy, proxyUsername, proxyPassword, geoLatitude, geoLongitude, timezone).ConfigureAwait(false);
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// Gets HTML content from an already loaded page or element.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="selector">Optional CSS selector for the element.</param>
    /// <param name="innerHtml">Return inner HTML instead of outer HTML.</param>
    /// <param name="asText">Return text content instead of markup.</param>
    /// <returns>Extracted markup or text.</returns>
    public static async Task<string> GetContentAsync(IPage page, string? selector = null, bool innerHtml = false, bool asText = false) {
        if (string.IsNullOrEmpty(selector)) {
            if (asText) {
                return await page.InnerTextAsync("html").ConfigureAwait(false);
            }
            return await page.ContentAsync().ConfigureAwait(false);
        }

        var locator = page.Locator(selector!);
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
            bool potentiallyHidden = await el.EvaluateAsync<bool>(@"el => {
                const check = node => {
                    if (!node) return false;
                    if (node.getAttribute('aria-hidden') === 'true' || node.hidden) {
                        return true;
                    }
                    const style = window.getComputedStyle(node);
                    if (!style) return false;
                    return style.display === 'none' || style.visibility === 'hidden' || parseFloat(style.opacity) === 0;
                };
                for (let n = el; n; n = n.parentElement) {
                    if (check(n)) return true;
                }
                return false;
            }");
            string selector = await el.EvaluateAsync<string>(@"el => {
                const esc = (CSS && CSS.escape) ? CSS.escape : (s => s);
                let sel = el.tagName.toLowerCase();
                if (el.id) return sel + '#' + esc(el.id);
                const href = el.getAttribute('href');
                if (href) return `${sel}[href='${href.replace(/'/g, ""\\'"")}']`;
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
                Visible = visible,
                PotentiallyHidden = potentiallyHidden
            });
        }
        return list;
    }

    /// <summary>
    /// Navigates the specified session to a new URL and waits for the network to be idle.
    /// </summary>
    public static async Task NavigateAsync(HtmlBrowserSession session, string url, int timeout = 10000) {
        await session.Page.GotoAsync(url, new PageGotoOptions { Timeout = timeout }).ConfigureAwait(false);
        await session.Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = timeout }).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element by CSS selector.
    /// </summary>
    public static async Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation = false, int timeout = 10000) {
        if (waitForNavigation) {
            Task waitTask = session.Page.WaitForURLAsync("**", new PageWaitForURLOptions { Timeout = timeout });
            await session.Page.ClickAsync(selector, new PageClickOptions { Timeout = timeout }).ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
        } else {
            await session.Page.ClickAsync(selector, new PageClickOptions { Timeout = timeout }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clicks an element specified by text content.
    /// </summary>
    public static async Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact = false, string? regex = null, bool waitForNavigation = false, int timeout = 10000) {
        ILocator locator = !string.IsNullOrEmpty(regex)
            ? session.Page.GetByText(new Regex(regex))
            : exact
                ? session.Page.GetByText(text, new PageGetByTextOptions { Exact = true })
                : session.Page.GetByText(text);

        if (waitForNavigation) {
            Task waitTask = session.Page.WaitForURLAsync("**", new PageWaitForURLOptions { Timeout = timeout });
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
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
