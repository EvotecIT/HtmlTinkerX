using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    internal static Func<Task<IPlaywright>>? PlaywrightFactory { get; set; }

    private static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchBrowserAsync(
        HtmlBrowserEngine browser,
        bool clean,
        bool headless,
        int slowMo,
        string? proxy,
        string? proxyUsername,
        string? proxyPassword,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (clean) {
            CleanInstallDir();
        }

        await EnsureInstalledAsync(browser).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var playwright = PlaywrightFactory != null
            ? await PlaywrightFactory().ConfigureAwait(false)
            : await Playwright.CreateAsync();

        IBrowserType type = browser switch {
            HtmlBrowserEngine.Firefox => playwright.Firefox,
            HtmlBrowserEngine.WebKit => playwright.Webkit,
            _ => playwright.Chromium,
        };

        var launchOptions = new BrowserTypeLaunchOptions {
            Headless = headless,
            SlowMo = slowMo
        };

        if (!string.IsNullOrEmpty(proxy)) {
            launchOptions.Proxy = new Proxy {
                Server = proxy!,
                Username = proxyUsername,
                Password = proxyPassword
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        var browserInstance = await type.LaunchAsync(launchOptions);
        return (playwright, browserInstance);
    }

    private static async Task<(IBrowserContext Context, IPage Page)> CreateBrowserContextAsync(
        IBrowser browserInstance,
        string? username,
        string? password,
        HtmlFormLogin? formLogin,
        string? videoPath,
        int videoWidth,
        int videoHeight,
        string? storageStatePath,
        string? userAgent,
        int? viewportWidth,
        int? viewportHeight,
        float? deviceScaleFactor,
        double? geoLatitude,
        double? geoLongitude,
        string? timezone,
        CancellationToken cancellationToken) {
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
            string resolved = HtmlUtilities.EnsureDirectoryExists(videoPath!);
            string dir = Path.GetDirectoryName(resolved) ?? resolved;
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

        cancellationToken.ThrowIfCancellationRequested();
        var context = await browserInstance.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();
        return (context, page);
    }

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
        string? timezone = null,
        IEnumerable<HtmlNetworkResourceType>? blockResourceTypes = null,
        IEnumerable<string>? blockResourcePatterns = null,
        HtmlBrowserLoadState loadState = HtmlBrowserLoadState.NetworkIdle,
        int timeout = 10000,
        CancellationToken cancellationToken = default) {
        var (playwright, browserInstance) = await LaunchBrowserAsync(
            browser,
            clean,
            headless,
            slowMo,
            proxy,
            proxyUsername,
            proxyPassword,
            cancellationToken);

        var (context, page) = await CreateBrowserContextAsync(
            browserInstance,
            username,
            password,
            formLogin,
            videoPath,
            videoWidth,
            videoHeight,
            storageStatePath,
            userAgent,
            viewportWidth,
            viewportHeight,
            deviceScaleFactor,
            geoLatitude,
            geoLongitude,
            timezone,
            cancellationToken);

        var network = new System.Collections.Concurrent.ConcurrentDictionary<IRequest, HtmlNetworkEntry>();
        HtmlBrowserSession session = new(
            playwright,
            browserInstance,
            context,
            page,
            !string.IsNullOrEmpty(videoPath) ? page.Video : null,
            videoPath,
            network);

        await ApplyResourceBlockingAsync(page, blockResourceTypes, blockResourcePatterns, cancellationToken).ConfigureAwait(false);
        await NavigateAsync(page, url, formLogin, username, password, loadState, timeout, cancellationToken);

        return session;
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
        string? timezone = null,
        int timeout = 10000,
        CancellationToken cancellationToken = default)
        => OpenSessionWithOptionsAsync(
            url,
            browser,
            clean,
            username,
            password,
            formLogin,
            headless,
            slowMo,
            videoPath,
            videoWidth,
            videoHeight,
            storageStatePath,
            userAgent,
            viewportWidth,
            viewportHeight,
            deviceScaleFactor,
            proxy,
            proxyUsername,
            proxyPassword,
            geoLatitude,
            geoLongitude,
            timezone,
            blockResourceTypes: null,
            blockResourcePatterns: null,
            loadState: HtmlBrowserLoadState.NetworkIdle,
            timeout,
            cancellationToken);

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> and navigates to the specified URL.
    /// </summary>
    public static Task<HtmlBrowserSession> OpenSessionWithOptionsAsync(
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
        string? timezone = null,
        IEnumerable<HtmlNetworkResourceType>? blockResourceTypes = null,
        IEnumerable<string>? blockResourcePatterns = null,
        HtmlBrowserLoadState loadState = HtmlBrowserLoadState.NetworkIdle,
        int timeout = 10000,
        CancellationToken cancellationToken = default)
        => CreatePageAsync(url, browser, clean, username, password, formLogin, headless, slowMo, videoPath, videoWidth, videoHeight, storageStatePath, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, proxy, proxyUsername, proxyPassword, geoLatitude, geoLongitude, timezone, blockResourceTypes, blockResourcePatterns, loadState, timeout, cancellationToken);

    /// <summary>
    /// Disposes the specified browser session.
    /// </summary>
    public static async Task CloseSessionAsync(HtmlBrowserSession session, CancellationToken cancellationToken = default) {
        if (session != null) {
            cancellationToken.ThrowIfCancellationRequested();
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
    /// <summary>
    /// Retrieves the fully rendered HTML from the specified URL after executing JavaScript.
    /// </summary>
    /// <param name="url">The URL to load.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Force re-download of browser runtimes.</param>
    /// <param name="username">Username for authentication.</param>
    /// <param name="password">Password for authentication.</param>
    /// <param name="formLogin">Form based login parameters.</param>
    /// <param name="headless">Run browser in headless mode.</param>
    /// <param name="slowMo">Slow motion delay in milliseconds.</param>
    /// <param name="userAgent">Custom user agent string.</param>
    /// <param name="viewportWidth">Viewport width in pixels.</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    /// <param name="deviceScaleFactor">Device scale factor.</param>
    /// <param name="proxy">Proxy server URL.</param>
    /// <param name="proxyUsername">Proxy username.</param>
    /// <param name="proxyPassword">Proxy password.</param>
    /// <param name="geoLatitude">Latitude for geolocation.</param>
    /// <param name="geoLongitude">Longitude for geolocation.</param>
    /// <param name="timezone">Timezone identifier.</param>
    /// <param name="timeout">Navigation timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered HTML markup.</returns>
    public static async Task<string> GetPageContentAsync(string url, HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium, bool clean = false, string? username = null, string? password = null, HtmlFormLogin? formLogin = null, bool headless = true, int slowMo = 0, string? userAgent = null, int? viewportWidth = null, int? viewportHeight = null, float? deviceScaleFactor = null, string? proxy = null, string? proxyUsername = null, string? proxyPassword = null, double? geoLatitude = null, double? geoLongitude = null, string? timezone = null, int timeout = 10000, CancellationToken cancellationToken = default) {
        await using HtmlBrowserSession session = await OpenSessionWithOptionsAsync(
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
            timezone: timezone,
            blockResourceTypes: null,
            blockResourcePatterns: null,
            loadState: HtmlBrowserLoadState.NetworkIdle,
            timeout: timeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await session.Page.ContentAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the rendered HTML to a file.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path to write.</param>
    /// <param name="browser">Browser engine to use.</param>
    /// <param name="clean">Force re-download of browser runtimes.</param>
    /// <param name="username">Username for authentication.</param>
    /// <param name="password">Password for authentication.</param>
    /// <param name="formLogin">Form based login parameters.</param>
    /// <param name="headless">Run browser in headless mode.</param>
    /// <param name="slowMo">Slow motion delay in milliseconds.</param>
    /// <param name="userAgent">Custom user agent string.</param>
    /// <param name="viewportWidth">Viewport width in pixels.</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    /// <param name="deviceScaleFactor">Device scale factor.</param>
    /// <param name="proxy">Proxy server URL.</param>
    /// <param name="proxyUsername">Proxy username.</param>
    /// <param name="proxyPassword">Proxy password.</param>
    /// <param name="geoLatitude">Latitude for geolocation.</param>
    /// <param name="geoLongitude">Longitude for geolocation.</param>
    /// <param name="timezone">Timezone identifier.</param>
    /// <param name="timeout">Navigation timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SavePageContentAsync(string url, string path, HtmlBrowserEngine browser = HtmlBrowserEngine.Chromium, bool clean = false, string? username = null, string? password = null, HtmlFormLogin? formLogin = null, bool headless = true, int slowMo = 0, string? userAgent = null, int? viewportWidth = null, int? viewportHeight = null, float? deviceScaleFactor = null, string? proxy = null, string? proxyUsername = null, string? proxyPassword = null, double? geoLatitude = null, double? geoLongitude = null, string? timezone = null, int timeout = 10000, CancellationToken cancellationToken = default) {
        string fullPath = path.ToFullPath();
        string content = await GetPageContentAsync(url, browser, clean, username, password, formLogin, headless, slowMo, userAgent, viewportWidth, viewportHeight, deviceScaleFactor, proxy, proxyUsername, proxyPassword, geoLatitude, geoLongitude, timezone, timeout, cancellationToken).ConfigureAwait(false);
#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(fullPath, content);
#else
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// Gets HTML content from an already loaded page or element.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="selector">Optional CSS selector for the element.</param>
    /// <param name="innerHtml">Return inner HTML instead of outer HTML.</param>
    /// <param name="asText">Return text content instead of markup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted markup or text.</returns>
    public static Task<string> GetContentAsync(IPage page, string? selector = null, bool innerHtml = false, bool asText = false, CancellationToken cancellationToken = default)
        => GetContentAsync(page, selector, innerHtml, asText, timeout: null, cancellationToken);

    /// <summary>
    /// Gets HTML content from an already loaded page or element.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="selector">Optional CSS selector for the element.</param>
    /// <param name="innerHtml">Return inner HTML instead of outer HTML.</param>
    /// <param name="asText">Return text content instead of markup.</param>
    /// <param name="timeout">Optional selector wait timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted markup or text.</returns>
    public static async Task<string> GetContentAsync(IPage page, string? selector, bool innerHtml, bool asText, int? timeout, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(selector)) {
            if (asText) {
                cancellationToken.ThrowIfCancellationRequested();
                return await page.InnerTextAsync("html").ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return await page.ContentAsync().ConfigureAwait(false);
        }

        var locator = page.Locator(selector!);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions {
            Timeout = timeout
        }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);

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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of interactable element descriptions.</returns>
    public static async Task<List<HtmlInteractableInfo>> GetInteractablesAsync(IPage page, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var elements = await page.QuerySelectorAllAsync("a,button,[role=button],input[type=button],input[type=submit]");
        List<HtmlInteractableInfo> list = new();
        int index = 0;
        foreach (var el in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            string rawText = await el.InnerTextAsync();
            string text = Regex.Replace(rawText, "\\s+", " ").Trim();
            string tag = await el.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
            string? href = await el.GetAttributeAsync("href");
            string? id = await el.GetAttributeAsync("id");
            string? cls = await el.GetAttributeAsync("class");
            bool visible = await el.IsVisibleAsync();
            bool enabled = await el.IsEnabledAsync();
            bool editable = await el.IsEditableAsync();
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
            HtmlBrowserElementInfo elementInfo = ParseElementInfo(await el.EvaluateAsync<string>(
                ElementInfoScript,
                new {
                    includeAttributes = false,
                    includeHtml = false
                }).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
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
                PotentiallyHidden = potentiallyHidden,
                Enabled = enabled,
                Editable = editable,
                InViewport = elementInfo.InViewport,
                X = elementInfo.X,
                Y = elementInfo.Y,
                Width = elementInfo.Width,
                Height = elementInfo.Height
            });
        }
        return list;
    }

    /// <summary>
    /// Clicks an element by CSS selector.
    /// </summary>
    public static Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation = false, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickSelectorAsync(session, selector, waitForNavigation, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Clicks an element by CSS selector, optionally targeting a zero-based matching element.
    /// </summary>
    public static async Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation, int timeout, CancellationToken cancellationToken, int? nth) {
        ILocator locator = session.Page.Locator(selector);
        if (nth.HasValue) {
            locator = locator.Nth(nth.Value);
        }

        if (waitForNavigation) {
            Task waitTask = session.Page.WaitForURLAsync("**", new PageWaitForURLOptions { Timeout = timeout });
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
        } else {
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clicks an element specified by text content.
    /// </summary>
    public static Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact = false, string? regex = null, bool waitForNavigation = false, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickTextAsync(session, text, exact, regex, waitForNavigation, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Clicks an element specified by text content, optionally targeting a zero-based matching element.
    /// </summary>
    public static async Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact, string? regex, bool waitForNavigation, int timeout, CancellationToken cancellationToken, int? nth) {
        ILocator locator = !string.IsNullOrEmpty(regex)
            ? session.Page.GetByText(new Regex(regex))
            : exact
                ? session.Page.GetByText(text, new PageGetByTextOptions { Exact = true })
                : session.Page.GetByText(text);
        if (nth.HasValue) {
            locator = locator.Nth(nth.Value);
        }

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
    /// <summary>
    /// Provides viewport and user agent settings for well known mobile devices.
    /// </summary>
    public static HtmlMobileDeviceInfo GetMobileDeviceInfo(HtmlMobileDevice device) => device switch {
        HtmlMobileDevice.IPhone12 => new HtmlMobileDeviceInfo {
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1",
            ViewportWidth = 390,
            ViewportHeight = 844
        },
        HtmlMobileDevice.Pixel5 => new HtmlMobileDeviceInfo {
            UserAgent = "Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0 Mobile Safari/537.36",
            ViewportWidth = 393,
            ViewportHeight = 851
        },
        HtmlMobileDevice.GalaxyS8 => new HtmlMobileDeviceInfo {
            UserAgent = "Mozilla/5.0 (Linux; Android 7.0; SM-G950U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0 Mobile Safari/537.36",
            ViewportWidth = 360,
            ViewportHeight = 740
        },
        _ => throw new System.ArgumentOutOfRangeException(nameof(device))
    };

    /// <summary>
    /// Applies mobile device emulation settings to an existing session.
    /// </summary>
    public static async Task SetMobileDeviceAsync(HtmlBrowserSession session, HtmlMobileDevice device, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new System.ArgumentNullException(nameof(session));
        }
        HtmlMobileDeviceInfo info = GetMobileDeviceInfo(device);
        cancellationToken.ThrowIfCancellationRequested();
        await session.Context.AddInitScriptAsync($"Object.defineProperty(navigator, 'userAgent', {{ get: () => '{info.UserAgent}' }});").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await session.Page.SetViewportSizeAsync(info.ViewportWidth, info.ViewportHeight).ConfigureAwait(false);
    }

}
