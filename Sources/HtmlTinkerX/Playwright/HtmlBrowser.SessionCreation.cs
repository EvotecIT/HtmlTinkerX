using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
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
        bool skipInitialNavigation = false) {
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
                (playwright, browserInstance) = await LaunchBrowserAsync(options, cancellationToken, policyLifetime?.Proxy.Server).ConfigureAwait(false);
                (context, page) = await CreateBrowserContextAsync(browserInstance, options, cancellationToken,
                    policyLifetimeToken: policyLifetime?.Token ?? default).ConfigureAwait(false);
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
        } catch {
            await AbortOwnershipAsync().ConfigureAwait(false);
            throw;
        }
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
            || options.Clean) {
            throw new ArgumentException("CdpEndpointUrl attaches to an already-running browser, so BrowserChannel, BrowserExecutablePath, and Clean are not used. BrowserArguments and ChromiumSandbox are not used either.");
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
