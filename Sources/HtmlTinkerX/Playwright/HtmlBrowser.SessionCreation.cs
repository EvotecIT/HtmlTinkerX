using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Creates a new Playwright browser session and navigates to the specified URL.
    /// </summary>
    private static async Task<HtmlBrowserSession> CreatePageAsync(
        string url,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken = default,
        bool skipInitialNavigation = false, bool allowCacheRepair = true) {
        ValidateSessionNetworkPolicy(options);
        if (options.NetworkPolicy != null && !skipInitialNavigation
            && !await new HtmlBrowserNetworkPolicyEvaluator(options.NetworkPolicy)
                .IsAllowedAsync(url, null, cancellationToken).ConfigureAwait(false)) {
            throw new UnauthorizedAccessException("The initial browser destination is denied by NetworkPolicy.");
        }
        if (!string.IsNullOrWhiteSpace(options.UserDataDirectory) && !string.IsNullOrWhiteSpace(options.StorageStatePath)) {
            throw new ArgumentException("Use either UserDataDirectory for a persistent profile or StorageStatePath for imported context state, not both.");
        }

        if (!string.IsNullOrWhiteSpace(options.CdpEndpointUrl)) {
            ValidateCdpAttachOptions(options);
        }

        if (options.HeaderProfile != null && (!string.IsNullOrWhiteSpace(options.UserAgent) || !string.IsNullOrWhiteSpace(options.Locale))) {
            throw new ArgumentException("HeaderProfile sets the User-Agent and locale; do not combine it with UserAgent or Locale.");
        }

        if (options.ManualLogin) {
            options.Headless = false;
        }

        IPlaywright? playwright = null;
        IBrowser? browserInstance = null;
        IBrowserContext? context = null;
        IPage? page = null;
        HtmlBrowserSessionNetworkPolicyLifetime? policyLifetime = null;
        string? resolvedUserDataDirectory = null;
        bool closeContextOnDispose = true;
        bool closeBrowserOnDispose = true;
        bool closePageOnDispose = false;
        bool pageScopedInitScripts = false;
        int ownershipAborted = 0;

        async Task AbortOwnershipAsync() {
            if (Interlocked.Exchange(ref ownershipAborted, 1) != 0) return;
            if (closePageOnDispose && page != null && !page.IsClosed) StartBestEffortClose(() => page.CloseAsync());
            if (closeContextOnDispose && context != null) StartBestEffortClose(() => context.CloseAsync());
            if (closeBrowserOnDispose && browserInstance != null && browserInstance.IsConnected) StartBestEffortClose(() => browserInstance.CloseAsync());
            try { playwright?.Dispose(); } catch (Exception) { }
            if (policyLifetime != null) await policyLifetime.DisposeAsync().ConfigureAwait(false);
        }

        try {
            if (!string.IsNullOrWhiteSpace(options.CdpEndpointUrl)) {
                (playwright, browserInstance) = await ConnectOverCdpAsync(options, cancellationToken).ConfigureAwait(false);
                closeContextOnDispose = false;
                closeBrowserOnDispose = false;
                closePageOnDispose = true;
                if (browserInstance.Contexts.Count > 0) {
                    context = browserInstance.Contexts[0];
                    pageScopedInitScripts = true;
                    Task<IPage> pageCreation = context.NewPageAsync();
                    try {
                        page = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                            () => pageCreation,
                            static () => Task.CompletedTask,
                            cancellationToken).ConfigureAwait(false);
                    } catch (OperationCanceledException) {
                        IPlaywright playwrightOwner = playwright!;
                        playwright = null;
                        ClosePageWhenCreated(pageCreation, playwrightOwner);
                        throw;
                    }
                } else {
                    closeContextOnDispose = true;
                    closePageOnDispose = false;
                    (context, page) = await CreateBrowserContextAsync(
                        browserInstance,
                        options,
                        cancellationToken,
                        closeBrowserOnCancellation: false,
                        contextCreationCanceled: contextCreation => {
                            IPlaywright playwrightOwner = playwright!;
                            playwright = null;
                            CloseContextWhenCreated(contextCreation, playwrightOwner);
                        }).ConfigureAwait(false);
                }
            } else if (!string.IsNullOrWhiteSpace(options.UserDataDirectory)) {
                resolvedUserDataDirectory = HtmlUtilities.EnsureDirectoryExists(options.UserDataDirectory!);
                (playwright, context, page) = await LaunchPersistentContextAsync(options, cancellationToken).ConfigureAwait(false);
                browserInstance = context.Browser;
                closeBrowserOnDispose = false;
            } else {
                if (options.NetworkPolicy != null) policyLifetime = new HtmlBrowserSessionNetworkPolicyLifetime(options.NetworkPolicy);
                var launch = await LaunchBrowserWithCacheDirectoryAsync(options, cancellationToken, policyLifetime?.Proxy.Server, allowCacheRepair).ConfigureAwait(false);
                playwright = launch.Playwright;
                browserInstance = launch.Browser;
                try {
                    (context, page) = await CreateBrowserContextAsync(browserInstance, options, cancellationToken,
                        policyLifetimeToken: policyLifetime?.Token ?? default).ConfigureAwait(false);
                } catch (PlaywrightException ex) {
                    if (!await RepairFailedCacheLaunchAsync(options, launch.CacheDirectory, ex, launch.AllowRepair).ConfigureAwait(false)) throw;
                    throw new BrowserCacheRepairedException(ex);
                }
            }

            if (options.HeaderProfile != null) {
                await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    () => ApplyHeaderProfileAsync(page!, options, attached: pageScopedInitScripts),
                    AbortOwnershipAsync,
                    cancellationToken).ConfigureAwait(false);
            }

            await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => pageScopedInitScripts
                    ? ApplyInitScriptsAsync(page!, options, cancellationToken)
                    : ApplyInitScriptsAsync(context!, options, cancellationToken),
                AbortOwnershipAsync,
                cancellationToken).ConfigureAwait(false);

            var network = new System.Collections.Concurrent.ConcurrentDictionary<IRequest, HtmlNetworkEntry>();
            HtmlBrowserSession session = new(
                playwright!,
                browserInstance,
                context!,
                page!,
                !string.IsNullOrWhiteSpace(options.VideoPath) ? page!.Video : null,
                options.VideoPath,
                network,
                resolvedUserDataDirectory,
                options.CdpEndpointUrl,
                closeContextOnDispose,
                closeBrowserOnDispose,
                closePageOnDispose);
            session.NetworkPolicyLifetime = policyLifetime;

            await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => ApplyResourceBlockingAsync(page!, options.BlockResourceTypes, options.BlockResourcePatterns, cancellationToken),
                AbortOwnershipAsync,
                cancellationToken).ConfigureAwait(false);
            await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => NavigateAsync(page!, url, options.FormLogin, options.Username, options.Password, options.LoadState, options.Timeout, cancellationToken, skipInitialNavigation),
                AbortOwnershipAsync,
                cancellationToken).ConfigureAwait(false);
            if (options.ManualLogin) {
                await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    () => WaitForManualLoginAsync(session, options.LoginSuccessSelector, options.LoginTimeout, cancellationToken),
                    AbortOwnershipAsync,
                    cancellationToken).ConfigureAwait(false);
            }

            return session;
        } catch (BrowserCacheRepairedException) when (allowCacheRepair) {
            await AbortOwnershipAsync().ConfigureAwait(false);
            return await CreatePageAsync(url, options, cancellationToken, skipInitialNavigation, allowCacheRepair: false).ConfigureAwait(false);
        } catch {
            await AbortOwnershipAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Makes the page send the profile's identity. Chromium takes the User-Agent, Accept-Language and client hints
    /// together through its user-agent override, which also keeps navigator.userAgentData consistent; other engines
    /// use the context's User-Agent and locale and send no client hints. An attached browser keeps its own identity,
    /// so the profile must be the one it reports.
    /// </summary>
    private static async Task ApplyHeaderProfileAsync(IPage page, HtmlBrowserLaunchOptions options, bool attached) {
        HtmlBrowserHeaderProfile profile = options.HeaderProfile!;
        if (attached) {
            string userAgent = await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false);
            if (!string.Equals(userAgent.Replace("HeadlessChrome/", "Chrome/"), profile.UserAgent, StringComparison.Ordinal)) {
                throw new ArgumentException("HeaderProfile describes a different browser than the one at CdpEndpointUrl. Read it from that browser with HtmlBrowser.GetHeaderProfileAsync.");
            }
        }
        if (options.Browser != HtmlBrowserEngine.Chromium) return;
        Dictionary<string, object> metadata = new() {
            ["brands"] = (profile.Brands ?? Array.Empty<KeyValuePair<string, string>>())
                .Select(static brand => new Dictionary<string, string> { ["brand"] = brand.Key, ["version"] = brand.Value }).ToArray(),
            ["platform"] = profile.Platform ?? string.Empty,
            ["platformVersion"] = string.Empty,
            ["architecture"] = string.Empty,
            ["model"] = string.Empty,
            ["mobile"] = profile.Mobile
        };
        ICDPSession cdp = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        await cdp.SendAsync("Emulation.setUserAgentOverride", new Dictionary<string, object> {
            ["userAgent"] = profile.UserAgent,
            // Chromium weighs the tags itself, the way a derived profile's Accept-Language is weighed.
            ["acceptLanguage"] = string.Join(",", LanguageTags(profile.AcceptLanguage)),
            ["userAgentMetadata"] = metadata
        }).ConfigureAwait(false);
    }

    private static void ValidateCdpAttachOptions(HtmlBrowserLaunchOptions options) {
        if (!string.IsNullOrWhiteSpace(options.UserDataDirectory)) {
            throw new ArgumentException("CdpEndpointUrl attaches to an already-running browser. Launch Chrome with the desired --user-data-dir before attaching; do not combine CdpEndpointUrl with UserDataDirectory.");
        }

        if (!string.IsNullOrWhiteSpace(options.StorageStatePath)) {
            throw new ArgumentException("CdpEndpointUrl attaches to an existing browser context. Do not combine CdpEndpointUrl with StatePath or StorageStatePath.");
        }

        if (!string.IsNullOrWhiteSpace(options.BrowserChannel)
            || !string.IsNullOrWhiteSpace(options.BrowserExecutablePath)
            || options.BrowserArguments.Count > 0
            || options.ChromiumSandbox.HasValue
            || options.BlockPopups
            || options.Clean) {
            throw new ArgumentException("CdpEndpointUrl attaches to an already-running browser, so BrowserChannel, BrowserExecutablePath, and Clean are not used. BrowserArguments, ChromiumSandbox and BlockPopups are not used either.");
        }

        if (HasCdpContextOnlyOptions(options)) {
            throw new ArgumentException("CdpEndpointUrl attaches to an existing browser context, so context options such as Proxy, UserAgent, Locale, viewport, geolocation, timezone, and permissions are not applied. IgnoreHTTPSErrors, HTTP credentials, and video recording are not applied either. Launch Chrome with those settings before attaching.");
        }
    }

    private static bool HasCdpContextOnlyOptions(HtmlBrowserLaunchOptions options) =>
        options.IgnoreHTTPSErrors
        || (!string.IsNullOrWhiteSpace(options.VideoPath))
        || (options.FormLogin == null && !string.IsNullOrEmpty(options.Username) && options.Password != null)
        || !string.IsNullOrWhiteSpace(options.Proxy)
        || !string.IsNullOrWhiteSpace(options.ProxyUsername)
        || !string.IsNullOrWhiteSpace(options.ProxyPassword)
        || !string.IsNullOrWhiteSpace(options.UserAgent)
        || !string.IsNullOrWhiteSpace(options.Locale)
        || options.ViewportWidth.HasValue
        || options.ViewportHeight.HasValue
        || options.ScreenWidth.HasValue
        || options.ScreenHeight.HasValue
        || options.DeviceScaleFactor.HasValue
        || options.IsMobile.HasValue
        || options.HasTouch.HasValue
        || options.GeoLatitude.HasValue
        || options.GeoLongitude.HasValue
        || !string.IsNullOrWhiteSpace(options.Timezone)
        || options.Permissions.Count > 0;

}
