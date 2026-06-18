using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Higher-level browser actions used by interactive extraction workflows.
/// </summary>
public static partial class HtmlBrowser {
    private static readonly string[] CommonOverlaySelectors = {
        "#onetrust-accept-btn-handler",
        "#onetrust-reject-all-handler",
        ".onetrust-close-btn-handler",
        "button[aria-label='Close']",
        "button[aria-label='close']",
        "[data-testid='cookie-policy-dialog-accept-button']",
        "[data-testid='uc-accept-all-button']"
    };

    private static readonly string[] CommonOverlayTexts = {
        "Accept",
        "Accept all",
        "I agree",
        "Agree",
        "Got it",
        "Close",
        "Continue"
    };

    /// <summary>
    /// Sends keyboard input to an element.
    /// </summary>
    public static async Task PressKeysAsync(HtmlBrowserSession session, string selector, string keys, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        ILocator locator = session.Page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        await locator.PressAsync(keys, new LocatorPressOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        await RecordRecipeStepAsync(session, new HtmlBrowserRecipeStep {
            Action = HtmlBrowserRecipeAction.Key,
            Selector = selector,
            Keys = keys,
            Timeout = timeout
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Hovers over an element.
    /// </summary>
    public static async Task HoverAsync(HtmlBrowserSession session, string selector, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        ILocator locator = session.Page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        await locator.HoverAsync(new LocatorHoverOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Scrolls an element into view.
    /// </summary>
    public static async Task ScrollIntoViewAsync(HtmlBrowserSession session, string selector, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        ILocator locator = session.Page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        await locator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits until text is visible on the page or under a selector.
    /// </summary>
    public static async Task WaitForTextAsync(HtmlBrowserSession session, string text, string selector = "body", bool exact = false, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        ILocator scope = session.Page.Locator(selector);
        ILocator locator = exact
            ? scope.GetByText(text, new LocatorGetByTextOptions { Exact = true }).First
            : scope.GetByText(text).First;
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions {
            State = WaitForSelectorState.Visible,
            Timeout = timeout
        }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        await RecordRecipeStepAsync(session, new HtmlBrowserRecipeStep {
            Action = HtmlBrowserRecipeAction.WaitText,
            Selector = selector,
            Text = text,
            Exact = exact,
            Timeout = timeout
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits until the document HTML remains unchanged for the requested stable interval.
    /// </summary>
    public static async Task WaitUntilStableAsync(HtmlBrowserSession session, int stableMilliseconds = 500, int pollMilliseconds = 100, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (stableMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(nameof(stableMilliseconds), "StableMilliseconds must be zero or greater.");
        }

        if (pollMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(pollMilliseconds), "PollMilliseconds must be greater than zero.");
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeout);
        string? previous = null;
        DateTimeOffset stableSince = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow <= deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            string current = await session.Page.EvaluateAsync<string>("() => document.documentElement.outerHTML").ConfigureAwait(false);
            if (!string.Equals(previous, current, StringComparison.Ordinal)) {
                previous = current;
                stableSince = DateTimeOffset.UtcNow;
            } else if ((DateTimeOffset.UtcNow - stableSince).TotalMilliseconds >= stableMilliseconds) {
                return;
            }

            await session.Page.WaitForTimeoutAsync(pollMilliseconds).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"The page did not remain stable for {stableMilliseconds}ms within {timeout}ms.");
    }

    /// <summary>
    /// Attempts to dismiss common cookie and modal overlays.
    /// </summary>
    public static Task<IReadOnlyList<string>> DismissCommonOverlaysAsync(HtmlBrowserSession session, int timeout = 1500, int interactionDelayMs = 150, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        return ApplyPageInteractionsAsync(
            session.Page,
            dismissSelectors: CommonOverlaySelectors,
            dismissTexts: CommonOverlayTexts,
            interactionDelayMs: interactionDelayMs,
            timeout: timeout,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Collects browser runtime, storage, console, and observed network diagnostics for the current page.
    /// </summary>
    public static async Task<HtmlBrowserDiagnostics> GetDiagnosticsAsync(HtmlBrowserSession session, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        cancellationToken.ThrowIfCancellationRequested();
        HtmlNetworkEntry[] networkEntries = session.NetworkLog.ToArray();
        HtmlConsoleEntry[] consoleErrors = session.ConsoleLog
            .Where(static entry => entry.Type == HtmlConsoleMessageType.Error)
            .ToArray();
        HtmlNetworkEntry[] failedRequests = networkEntries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.FailureText))
            .ToArray();
        var cookies = await session.Context.CookiesAsync().ConfigureAwait(false);
        string userAgent = await session.Page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false);
        string language = await session.Page.EvaluateAsync<string>("() => navigator.language || ''").ConfigureAwait(false);
        string platform = await session.Page.EvaluateAsync<string>("() => navigator.platform || ''").ConfigureAwait(false);
        bool webDriver = await session.Page.EvaluateAsync<bool>("() => navigator.webdriver === true").ConfigureAwait(false);
        int viewportWidth = await session.Page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false);
        int viewportHeight = await session.Page.EvaluateAsync<int>("() => window.innerHeight").ConfigureAwait(false);
        double devicePixelRatio = await session.Page.EvaluateAsync<double>("() => window.devicePixelRatio || 1").ConfigureAwait(false);
        string timezone = await session.Page.EvaluateAsync<string>("() => Intl.DateTimeFormat().resolvedOptions().timeZone || ''").ConfigureAwait(false);
        List<string> storageWarnings = new();
        string[] localStorageKeys = await GetStorageKeysOrEmptyAsync(session, "localStorage", storageWarnings).ConfigureAwait(false);
        string[] sessionStorageKeys = await GetStorageKeysOrEmptyAsync(session, "sessionStorage", storageWarnings).ConfigureAwait(false);
        IReadOnlyList<string> consistencyWarnings = BuildConsistencyWarnings(
            userAgent,
            language,
            platform,
            webDriver,
            viewportWidth,
            viewportHeight,
            devicePixelRatio,
            timezone,
            failedRequests,
            consoleErrors)
            .Concat(storageWarnings)
            .ToArray();

        return new HtmlBrowserDiagnostics {
            Url = session.Page.Url,
            Title = await session.Page.TitleAsync().ConfigureAwait(false),
            UserAgent = userAgent,
            Language = language,
            Platform = platform,
            WebDriver = webDriver,
            CookiesEnabled = await session.Page.EvaluateAsync<bool>("() => navigator.cookieEnabled === true").ConfigureAwait(false),
            Online = await session.Page.EvaluateAsync<bool>("() => navigator.onLine === true").ConfigureAwait(false),
            ViewportWidth = viewportWidth,
            ViewportHeight = viewportHeight,
            DevicePixelRatio = devicePixelRatio,
            Timezone = timezone,
            LocalStorageKeys = localStorageKeys,
            SessionStorageKeys = sessionStorageKeys,
            CookieCount = cookies.Count,
            NetworkEntryCount = networkEntries.Length,
            ObservedApiCalls = networkEntries
                .Where(static entry => entry.ResourceType == HtmlNetworkResourceType.XHR || entry.ResourceType == HtmlNetworkResourceType.Fetch)
                .ToArray(),
            FailedRequests = failedRequests,
            WebSocketEntries = networkEntries
                .Where(static entry => entry.ResourceType == HtmlNetworkResourceType.WebSocket)
                .ToArray(),
            ConsoleErrors = consoleErrors,
            ConsistencyWarnings = consistencyWarnings,
            FingerprintRiskScore = Math.Min(100, consistencyWarnings.Count * 20)
        };
    }

    private static async Task<string[]> GetStorageKeysOrEmptyAsync(HtmlBrowserSession session, string storageName, List<string> warnings) {
        try {
            return await session.Page.EvaluateAsync<string[]>($"() => Object.keys(window.{storageName} || {{}})").ConfigureAwait(false);
        } catch (PlaywrightException ex) when (IsStorageAccessDenied(ex)) {
            warnings.Add($"{storageName} access was denied for this page origin.");
            return Array.Empty<string>();
        }
    }

    private static bool IsStorageAccessDenied(PlaywrightException exception) =>
        exception.Message.IndexOf("SecurityError", StringComparison.OrdinalIgnoreCase) >= 0
        || exception.Message.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0
        || exception.Message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;

    private static IReadOnlyList<string> BuildConsistencyWarnings(
        string userAgent,
        string language,
        string platform,
        bool webDriver,
        int viewportWidth,
        int viewportHeight,
        double devicePixelRatio,
        string timezone,
        IReadOnlyList<HtmlNetworkEntry> failedRequests,
        IReadOnlyList<HtmlConsoleEntry> consoleErrors) {
        List<string> warnings = new();
        if (webDriver) {
            warnings.Add("navigator.webdriver is true.");
        }

        if (string.IsNullOrWhiteSpace(language)) {
            warnings.Add("navigator.language is empty.");
        }

        if (string.IsNullOrWhiteSpace(timezone)) {
            warnings.Add("Browser timezone could not be resolved.");
        }

        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) && viewportWidth > 900) {
            warnings.Add("User agent looks mobile while viewport is desktop-sized.");
        }

        if (!userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) && viewportWidth < 500 && viewportHeight < 1000) {
            warnings.Add("User agent looks desktop while viewport is phone-sized.");
        }

        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) && platform.Contains("Mac", StringComparison.OrdinalIgnoreCase)) {
            warnings.Add("User agent reports Windows while navigator.platform reports Mac.");
        }

        if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) && platform.Contains("Win", StringComparison.OrdinalIgnoreCase)) {
            warnings.Add("User agent reports macOS while navigator.platform reports Windows.");
        }

        if (devicePixelRatio <= 0) {
            warnings.Add("Device pixel ratio is not positive.");
        }

        if (failedRequests.Count > 0) {
            warnings.Add($"Failed or blocked requests observed: {failedRequests.Count}.");
        }

        if (consoleErrors.Count > 0) {
            warnings.Add($"Console errors observed: {consoleErrors.Count}.");
        }

        return warnings;
    }

    /// <summary>
    /// Clicks an element by CSS selector.
    /// </summary>
    public static Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation = false, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickSelectorAsync(session, selector, waitForNavigation, HtmlBrowserLoadState.NetworkIdle, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Clicks an element by CSS selector and optionally waits for the requested navigation load state.
    /// </summary>
    public static Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation, HtmlBrowserLoadState loadState, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickSelectorAsync(session, selector, waitForNavigation, loadState, navigationUrl: null, timeout, cancellationToken);

    /// <summary>
    /// Clicks an element by CSS selector and optionally waits for the requested navigation load state and URL glob.
    /// </summary>
    public static Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation, HtmlBrowserLoadState loadState, string? navigationUrl, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickSelectorAsync(session, selector, waitForNavigation, loadState, navigationUrl, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Clicks an element by CSS selector, optionally targeting a zero-based matching element.
    /// </summary>
    public static async Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation, int timeout, CancellationToken cancellationToken, int? nth) {
        await ClickSelectorAsync(session, selector, waitForNavigation, HtmlBrowserLoadState.NetworkIdle, timeout, cancellationToken, nth).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element by CSS selector, optionally targeting a zero-based matching element and waiting for a navigation load state.
    /// </summary>
    public static async Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation, HtmlBrowserLoadState loadState, int timeout, CancellationToken cancellationToken, int? nth) {
        await ClickSelectorAsync(session, selector, waitForNavigation, loadState, navigationUrl: null, timeout, cancellationToken, nth).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element by CSS selector, optionally targeting a zero-based matching element and waiting for a navigation load state and URL glob.
    /// </summary>
    public static async Task ClickSelectorAsync(HtmlBrowserSession session, string selector, bool waitForNavigation, HtmlBrowserLoadState loadState, string? navigationUrl, int timeout, CancellationToken cancellationToken, int? nth) {
        ILocator locator = session.Page.Locator(selector);
        if (nth.HasValue) {
            locator = locator.Nth(nth.Value);
        }

        if (waitForNavigation) {
            Task waitTask = WaitForUrlChangeAsync(session, loadState, navigationUrl, timeout);
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await waitTask.WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        } else {
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        await RecordRecipeStepAsync(session, new HtmlBrowserRecipeStep {
            Action = HtmlBrowserRecipeAction.Click,
            Selector = selector,
            WaitForNavigation = waitForNavigation,
            NavigationUrl = navigationUrl,
            LoadState = loadState,
            Timeout = timeout
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element specified by text content.
    /// </summary>
    public static Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact = false, string? regex = null, bool waitForNavigation = false, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickTextAsync(session, text, exact, regex, waitForNavigation, HtmlBrowserLoadState.NetworkIdle, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Clicks an element specified by text content and optionally waits for the requested navigation load state.
    /// </summary>
    public static Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact, string? regex, bool waitForNavigation, HtmlBrowserLoadState loadState, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickTextAsync(session, text, exact, regex, waitForNavigation, loadState, navigationUrl: null, timeout, cancellationToken);

    /// <summary>
    /// Clicks an element specified by text content and optionally waits for the requested navigation load state and URL glob.
    /// </summary>
    public static Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact, string? regex, bool waitForNavigation, HtmlBrowserLoadState loadState, string? navigationUrl, int timeout = 10000, CancellationToken cancellationToken = default) =>
        ClickTextAsync(session, text, exact, regex, waitForNavigation, loadState, navigationUrl, timeout, cancellationToken, nth: null);

    /// <summary>
    /// Clicks an element specified by text content, optionally targeting a zero-based matching element.
    /// </summary>
    public static async Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact, string? regex, bool waitForNavigation, int timeout, CancellationToken cancellationToken, int? nth) {
        await ClickTextAsync(session, text, exact, regex, waitForNavigation, HtmlBrowserLoadState.NetworkIdle, timeout, cancellationToken, nth).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element specified by text content, optionally targeting a zero-based matching element and waiting for a navigation load state.
    /// </summary>
    public static async Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact, string? regex, bool waitForNavigation, HtmlBrowserLoadState loadState, int timeout, CancellationToken cancellationToken, int? nth) {
        await ClickTextAsync(session, text, exact, regex, waitForNavigation, loadState, navigationUrl: null, timeout, cancellationToken, nth).ConfigureAwait(false);
    }

    /// <summary>
    /// Clicks an element specified by text content, optionally targeting a zero-based matching element and waiting for a navigation load state and URL glob.
    /// </summary>
    public static async Task ClickTextAsync(HtmlBrowserSession session, string text, bool exact, string? regex, bool waitForNavigation, HtmlBrowserLoadState loadState, string? navigationUrl, int timeout, CancellationToken cancellationToken, int? nth) {
        ILocator locator = !string.IsNullOrEmpty(regex)
            ? session.Page.GetByText(new Regex(regex))
            : exact
                ? session.Page.GetByText(text, new PageGetByTextOptions { Exact = true })
                : session.Page.GetByText(text);
        if (nth.HasValue) {
            locator = locator.Nth(nth.Value);
        }

        if (waitForNavigation) {
            Task waitTask = WaitForUrlChangeAsync(session, loadState, navigationUrl, timeout);
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            await waitTask.WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        } else {
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        RecordRecipeStep(session, new HtmlBrowserRecipeStep {
            Action = HtmlBrowserRecipeAction.ClickText,
            Text = text,
            Exact = exact,
            WaitForNavigation = waitForNavigation,
            NavigationUrl = navigationUrl,
            LoadState = loadState,
            Timeout = timeout
        });
    }

    private static Task WaitForUrlChangeAsync(HtmlBrowserSession session, HtmlBrowserLoadState loadState, string? navigationUrl, int timeout) {
        string startingUrl = session.Page.Url;
        return session.Page.WaitForURLAsync(
            url => !string.Equals(url, startingUrl, StringComparison.Ordinal) && MatchesNavigationUrl(url, navigationUrl),
            new PageWaitForURLOptions {
                Timeout = timeout,
                WaitUntil = ToWaitUntilState(loadState)
            });
    }

    private static bool MatchesNavigationUrl(string url, string? navigationUrl) {
        if (string.IsNullOrWhiteSpace(navigationUrl)) {
            return true;
        }

        string trimmedNavigationUrl = navigationUrl!.Trim();
        string pattern = "^" + Regex.Escape(trimmedNavigationUrl)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*") + "$";
        return Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
