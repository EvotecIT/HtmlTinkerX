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
    private static readonly TimeSpan LateCdpPageCleanupTimeout = TimeSpan.FromSeconds(2);
    internal static Func<Task<IPlaywright>>? PlaywrightFactory { get; set; }

    internal static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchBrowserAsync(
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken,
        string? policyProxyServer = null, bool allowCacheRepair = true) {
        var owner = await LaunchBrowserWithCacheDirectoryAsync(options, cancellationToken, policyProxyServer, allowCacheRepair).ConfigureAwait(false);
        return (owner.Playwright, owner.Browser);
    }

    private static async Task<(IPlaywright Playwright, IBrowser Browser, string? CacheDirectory, bool AllowRepair)> LaunchBrowserWithCacheDirectoryAsync(
        HtmlBrowserLaunchOptions options, CancellationToken cancellationToken, string? policyProxyServer = null, bool allowCacheRepair = true) {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Clean && allowCacheRepair) {
            await ExecuteSetupStageAsync(() => RepairInstallationAsync(options.Browser), cancellationToken).ConfigureAwait(false);
        }

        await ExecuteSetupStageAsync(
            () => EnsureBrowserRuntimeAvailableAsync(options),
            cancellationToken).ConfigureAwait(false);

        IPlaywright playwright = await CreatePlaywrightAsync(cancellationToken, options).ConfigureAwait(false);
        int playwrightDisposed = 0;
        Task DisposeOwnerAsync() {
            if (Interlocked.Exchange(ref playwrightDisposed, 1) == 0) playwright.Dispose();
            return Task.CompletedTask;
        }
        int lateLaunchCleanupOwnsPlaywright = 0;
        try {
            IBrowserType type = ResolveBrowserType(playwright, options.Browser);
            BrowserTypeLaunchOptions launchOptions = CreateLaunchOptions(options);
            string? cacheDirectory = null;
            if (policyProxyServer != null) ApplySessionPolicyLaunchOptions(launchOptions, policyProxyServer);
            Task<IBrowser> launch = LaunchWithCacheRepairAsync(options, async slot => {
                cacheDirectory = slot;
                if (slot != null)
                    launchOptions.ExecutablePath = GetManagedExecutable(options.Browser, options.Headless, slot);
                return await type.LaunchAsync(launchOptions).ConfigureAwait(false);
            }, allowCacheRepair);
            IBrowser browserInstance = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => launch,
                () => {
                    if (Interlocked.Exchange(ref lateLaunchCleanupOwnsPlaywright, 1) == 0) {
                        CloseBrowserWhenLaunchCompletes(launch, playwright);
                    }
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);
            return (playwright, browserInstance, cacheDirectory, allowCacheRepair);
        } catch (BrowserCacheRepairedException) when (allowCacheRepair) {
            await DisposeOwnerAsync().ConfigureAwait(false);
            return await LaunchBrowserWithCacheDirectoryAsync(options, cancellationToken, policyProxyServer, allowCacheRepair: false).ConfigureAwait(false);
        } catch {
            if (Volatile.Read(ref lateLaunchCleanupOwnsPlaywright) == 0) {
                await DisposeOwnerAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    private static async Task<(IPlaywright Playwright, IBrowser Browser)> ConnectOverCdpAsync(
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Browser != HtmlBrowserEngine.Chromium) {
            throw new ArgumentException("CDP attach is only supported for Chromium-based browsers. Use Browser Chromium with CdpEndpointUrl.", nameof(options));
        }

        if (PlaywrightFactory == null) {
            await ExecuteSetupStageAsync(EnsureDriverInstalledAsync, cancellationToken).ConfigureAwait(false);
        }

        IPlaywright playwright = await CreatePlaywrightAsync(cancellationToken).ConfigureAwait(false);
        int playwrightDisposed = 0;
        Task DisposeOwnerAsync() {
            if (Interlocked.Exchange(ref playwrightDisposed, 1) == 0) playwright.Dispose();
            return Task.CompletedTask;
        }
        try {
            IBrowser browserInstance = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => playwright.Chromium.ConnectOverCDPAsync(
                    options.CdpEndpointUrl!,
                    new BrowserTypeConnectOverCDPOptions {
                        SlowMo = options.SlowMo,
                        Timeout = options.Timeout
                    }),
                DisposeOwnerAsync,
                cancellationToken).ConfigureAwait(false);

            return (playwright, browserInstance);
        } catch {
            await DisposeOwnerAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<(IPlaywright Playwright, IBrowserContext Context, IPage Page)> LaunchPersistentContextAsync(
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken, bool allowCacheRepair = true) {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Clean && allowCacheRepair) {
            await ExecuteSetupStageAsync(() => RepairInstallationAsync(options.Browser), cancellationToken).ConfigureAwait(false);
        }

        await ExecuteSetupStageAsync(
            () => EnsureBrowserRuntimeAvailableAsync(options),
            cancellationToken).ConfigureAwait(false);

        string userDataDirectory = HtmlUtilities.EnsureDirectoryExists(options.UserDataDirectory!);

        IPlaywright playwright = await CreatePlaywrightAsync(cancellationToken, options).ConfigureAwait(false);
        int playwrightDisposed = 0;
        Task DisposeOwnerAsync() {
            if (Interlocked.Exchange(ref playwrightDisposed, 1) == 0) playwright.Dispose();
            return Task.CompletedTask;
        }
        IBrowserContext? context = null;
        int lateLaunchCleanupOwnsPlaywright = 0;
        try {
            IBrowserType type = ResolveBrowserType(playwright, options.Browser);
            BrowserTypeLaunchPersistentContextOptions contextOptions = CreatePersistentContextOptions(options);
            Task<IBrowserContext> launch = LaunchWithCacheRepairAsync(options, async slot => {
                if (slot != null)
                    contextOptions.ExecutablePath = GetManagedExecutable(options.Browser, options.Headless, slot);
                return await type.LaunchPersistentContextAsync(userDataDirectory, contextOptions).ConfigureAwait(false);
            }, allowCacheRepair);
            context = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => launch,
                () => {
                    if (Interlocked.Exchange(ref lateLaunchCleanupOwnsPlaywright, 1) == 0) {
                        ClosePersistentContextWhenLaunchCompletes(launch, playwright);
                    }
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);
            IPage page = context.Pages.Count > 0
                ? context.Pages[0]
                : await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    context.NewPageAsync,
                    () => context.CloseAsync(),
                    cancellationToken).ConfigureAwait(false);

            return (playwright, context, page);
        } catch (BrowserCacheRepairedException) when (allowCacheRepair) {
            await DisposeOwnerAsync().ConfigureAwait(false);
            return await LaunchPersistentContextAsync(options, cancellationToken, allowCacheRepair: false).ConfigureAwait(false);
        } catch {
            if (context != null) {
                StartBestEffortClose(() => context.CloseAsync());
            }
            if (Volatile.Read(ref lateLaunchCleanupOwnsPlaywright) == 0) {
                await DisposeOwnerAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    private static IBrowserType ResolveBrowserType(IPlaywright playwright, HtmlBrowserEngine browser) =>
        browser switch {
            HtmlBrowserEngine.Firefox => playwright.Firefox,
            HtmlBrowserEngine.WebKit => playwright.Webkit,
            _ => playwright.Chromium,
        };

    internal static bool ShouldInstallBundledRuntime(HtmlBrowserLaunchOptions options) =>
        string.IsNullOrWhiteSpace(options.BrowserChannel)
        && string.IsNullOrWhiteSpace(options.BrowserExecutablePath);

    private static Task EnsureBrowserRuntimeAvailableAsync(HtmlBrowserLaunchOptions options) =>
        ShouldInstallBundledRuntime(options)
            ? EnsureInstalledAsync(options.Browser)
            : EnsureDriverInstalledAsync();

    private static Task ExecuteSetupStageAsync(Func<Task> operation, CancellationToken cancellationToken) =>
        HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
            operation,
            static () => Task.CompletedTask,
            cancellationToken);

    private static async Task<IPlaywright> CreatePlaywrightAsync(CancellationToken cancellationToken, HtmlBrowserLaunchOptions? options = null) {
        cancellationToken.ThrowIfCancellationRequested();
        Task<IPlaywright> creation = PlaywrightFactory != null
            ? PlaywrightFactory()
            : CreateCachePlaywrightAsync(options);
        int lateCleanupStarted = 0;
        return await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
            () => creation,
            () => {
                if (Interlocked.Exchange(ref lateCleanupStarted, 1) == 0) {
                    DisposePlaywrightWhenCreated(creation);
                }
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal static BrowserTypeLaunchOptions CreateLaunchOptions(HtmlBrowserLaunchOptions options) {
        var launchOptions = new BrowserTypeLaunchOptions {
            Headless = options.Headless,
            SlowMo = options.SlowMo
        };

        if (!string.IsNullOrWhiteSpace(options.BrowserChannel)) {
            launchOptions.Channel = options.BrowserChannel;
        }

        if (!string.IsNullOrWhiteSpace(options.BrowserExecutablePath)) {
            launchOptions.ExecutablePath = options.BrowserExecutablePath!.ToFullPath();
        }

        if (options.BrowserArguments.Count > 0) {
            launchOptions.Args = options.BrowserArguments;
        }

        if (options.ChromiumSandbox.HasValue) {
            launchOptions.ChromiumSandbox = options.ChromiumSandbox.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.Proxy)) {
            launchOptions.Proxy = new Proxy {
                Server = options.Proxy!,
                Username = options.ProxyUsername,
                Password = options.ProxyPassword
            };
        }

        return launchOptions;
    }

    internal static BrowserTypeLaunchPersistentContextOptions CreatePersistentContextOptions(HtmlBrowserLaunchOptions options) {
        BrowserTypeLaunchPersistentContextOptions contextOptions = new() {
            Headless = options.Headless,
            SlowMo = options.SlowMo,
            IgnoreHTTPSErrors = options.IgnoreHTTPSErrors
        };

        ApplySharedContextOptions(
            contextOptions,
            options,
            setStorageState: false);
        if (options.BlockServiceWorkers || options.NetworkPolicy != null) contextOptions.ServiceWorkers = ServiceWorkerPolicy.Block;

        if (options.FormLogin == null && !string.IsNullOrEmpty(options.Username) && options.Password != null) {
            contextOptions.HttpCredentials = new HttpCredentials {
                Username = options.Username!,
                Password = options.Password!,
                Origin = options.HttpCredentialOrigin
            };
        }

        if (!string.IsNullOrWhiteSpace(options.BrowserChannel)) {
            contextOptions.Channel = options.BrowserChannel;
        }

        if (!string.IsNullOrWhiteSpace(options.BrowserExecutablePath)) {
            contextOptions.ExecutablePath = options.BrowserExecutablePath!.ToFullPath();
        }

        if (options.BrowserArguments.Count > 0) {
            contextOptions.Args = options.BrowserArguments;
        }

        if (options.ChromiumSandbox.HasValue) {
            contextOptions.ChromiumSandbox = options.ChromiumSandbox.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.Proxy)) {
            contextOptions.Proxy = new Proxy {
                Server = options.Proxy!,
                Username = options.ProxyUsername,
                Password = options.ProxyPassword
            };
        }

        return contextOptions;
    }

    private static async Task<(IBrowserContext Context, IPage Page)> CreateBrowserContextAsync(
        IBrowser browserInstance,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken,
        bool closeBrowserOnCancellation = true,
        Action<Task<IBrowserContext>>? contextCreationCanceled = null,
        CancellationToken policyLifetimeToken = default) {
        BrowserNewContextOptions? contextOptions = null;
        if (options.FormLogin == null && !string.IsNullOrEmpty(options.Username) && options.Password != null) {
            contextOptions = new BrowserNewContextOptions {
                HttpCredentials = new HttpCredentials {
                    Username = options.Username!,
                    Password = options.Password!,
                Origin = options.HttpCredentialOrigin
                }
            };
        }

        contextOptions ??= new BrowserNewContextOptions();
        contextOptions.IgnoreHTTPSErrors = options.IgnoreHTTPSErrors;

        ApplySharedContextOptions(contextOptions, options, setStorageState: true);
        if (options.BlockServiceWorkers || options.NetworkPolicy != null) contextOptions.ServiceWorkers = ServiceWorkerPolicy.Block;

        Task<IBrowserContext> contextCreation = browserInstance.NewContextAsync(contextOptions);
        IBrowserContext context;
        try {
            context = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => contextCreation,
                closeBrowserOnCancellation ? () => browserInstance.CloseAsync() : static () => Task.CompletedTask,
                cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (!closeBrowserOnCancellation) {
            if (contextCreationCanceled != null) {
                contextCreationCanceled(contextCreation);
            } else {
                CloseContextWhenCreated(contextCreation);
            }
            throw;
        }
        try {
            if (options.NetworkPolicy != null) {
                await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    () => InstallSessionNetworkPolicyAsync(context, options.NetworkPolicy, policyLifetimeToken),
                    () => context.CloseAsync(), cancellationToken).ConfigureAwait(false);
            }
            IPage page = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                context.NewPageAsync,
                () => context.CloseAsync(),
                cancellationToken).ConfigureAwait(false);
            return (context, page);
        } catch {
            StartBestEffortClose(() => context.CloseAsync());
            throw;
        }
    }

    internal static void CloseContextWhenCreated(
        Task<IBrowserContext> contextCreation,
        IPlaywright? playwrightOwner = null,
        TimeSpan? cleanupTimeout = null) =>
        StartBestEffortClose(async () => {
            try {
                Task deadline = Task.Delay(cleanupTimeout ?? LateCdpPageCleanupTimeout);
                if (await Task.WhenAny(contextCreation, deadline).ConfigureAwait(false) == contextCreation) {
                    IBrowserContext context = await contextCreation.ConfigureAwait(false);
                    Task close = context.CloseAsync();
                    if (await Task.WhenAny(close, deadline).ConfigureAwait(false) == close) {
                        await close.ConfigureAwait(false);
                    } else {
                        ObserveLateFault(close);
                    }
                } else {
                    ObserveLateFault(contextCreation);
                }
            } finally {
                // Disposing this local owner disconnects only the CDP client and guarantees
                // cleanup even when the externally owned browser never answers NewContext.
                playwrightOwner?.Dispose();
            }
        });

    internal static void ClosePageWhenCreated(
        Task<IPage> pageCreation,
        IPlaywright playwrightOwner,
        TimeSpan? cleanupTimeout = null) =>
        StartBestEffortClose(async () => {
            try {
                Task deadline = Task.Delay(cleanupTimeout ?? LateCdpPageCleanupTimeout);
                if (await Task.WhenAny(pageCreation, deadline).ConfigureAwait(false) == pageCreation) {
                    IPage page = await pageCreation.ConfigureAwait(false);
                    Task close = page.CloseAsync();
                    if (await Task.WhenAny(close, deadline).ConfigureAwait(false) == close) {
                        await close.ConfigureAwait(false);
                    } else {
                        ObserveLateFault(close);
                    }
                } else {
                    ObserveLateFault(pageCreation);
                }
            } finally {
                // Disposing the local Playwright owner only disconnects this CDP client. It
                // bounds cleanup even when the externally owned browser never answers NewPage.
                playwrightOwner.Dispose();
            }
        });

    private static void ObserveLateFault(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static void ApplySharedContextOptions(BrowserNewContextOptions contextOptions, HtmlBrowserLaunchOptions options, bool setStorageState) {
        if (setStorageState && !string.IsNullOrWhiteSpace(options.StorageStatePath)) {
            contextOptions.StorageStatePath = options.StorageStatePath;
        }

        if (!string.IsNullOrWhiteSpace(options.VideoPath)) {
            string resolved = HtmlUtilities.EnsureDirectoryExists(options.VideoPath!);
            string dir = Path.GetDirectoryName(resolved) ?? resolved;
            contextOptions.RecordVideoDir = dir;
            contextOptions.RecordVideoSize = new RecordVideoSize { Width = options.VideoWidth, Height = options.VideoHeight };
        }

        if (!string.IsNullOrWhiteSpace(options.UserAgent)) {
            contextOptions.UserAgent = options.UserAgent;
        }

        if (!string.IsNullOrWhiteSpace(options.Locale)) {
            contextOptions.Locale = options.Locale;
        }

        if (options.ViewportWidth.HasValue && options.ViewportHeight.HasValue) {
            contextOptions.ViewportSize = new ViewportSize { Width = options.ViewportWidth.Value, Height = options.ViewportHeight.Value };
        }

        if (options.ScreenWidth.HasValue && options.ScreenHeight.HasValue) {
            contextOptions.ScreenSize = new ScreenSize { Width = options.ScreenWidth.Value, Height = options.ScreenHeight.Value };
        }

        if (options.DeviceScaleFactor.HasValue) {
            contextOptions.DeviceScaleFactor = options.DeviceScaleFactor.Value;
        }

        if (options.IsMobile.HasValue) {
            contextOptions.IsMobile = options.IsMobile.Value;
        }

        if (options.HasTouch.HasValue) {
            contextOptions.HasTouch = options.HasTouch.Value;
        }

        if (options.GeoLatitude.HasValue && options.GeoLongitude.HasValue) {
            contextOptions.Geolocation = new Geolocation {
                Latitude = (float)options.GeoLatitude.Value,
                Longitude = (float)options.GeoLongitude.Value,
                Accuracy = 0
            };
        }

        if (options.Permissions.Count > 0) {
            contextOptions.Permissions = options.Permissions;
        } else if (options.GeoLatitude.HasValue && options.GeoLongitude.HasValue) {
            contextOptions.Permissions = new[] { "geolocation" };
        }

        if (!string.IsNullOrWhiteSpace(options.Timezone)) {
            contextOptions.TimezoneId = options.Timezone;
        }
    }

    private static void ApplySharedContextOptions(BrowserTypeLaunchPersistentContextOptions contextOptions, HtmlBrowserLaunchOptions options, bool setStorageState) {
        if (!string.IsNullOrWhiteSpace(options.VideoPath)) {
            string resolved = HtmlUtilities.EnsureDirectoryExists(options.VideoPath!);
            string dir = Path.GetDirectoryName(resolved) ?? resolved;
            contextOptions.RecordVideoDir = dir;
            contextOptions.RecordVideoSize = new RecordVideoSize { Width = options.VideoWidth, Height = options.VideoHeight };
        }

        if (!string.IsNullOrWhiteSpace(options.UserAgent)) {
            contextOptions.UserAgent = options.UserAgent;
        }

        if (!string.IsNullOrWhiteSpace(options.Locale)) {
            contextOptions.Locale = options.Locale;
        }

        if (options.ViewportWidth.HasValue && options.ViewportHeight.HasValue) {
            contextOptions.ViewportSize = new ViewportSize { Width = options.ViewportWidth.Value, Height = options.ViewportHeight.Value };
        }

        if (options.ScreenWidth.HasValue && options.ScreenHeight.HasValue) {
            contextOptions.ScreenSize = new ScreenSize { Width = options.ScreenWidth.Value, Height = options.ScreenHeight.Value };
        }

        if (options.DeviceScaleFactor.HasValue) {
            contextOptions.DeviceScaleFactor = options.DeviceScaleFactor.Value;
        }

        if (options.IsMobile.HasValue) {
            contextOptions.IsMobile = options.IsMobile.Value;
        }

        if (options.HasTouch.HasValue) {
            contextOptions.HasTouch = options.HasTouch.Value;
        }

        if (options.GeoLatitude.HasValue && options.GeoLongitude.HasValue) {
            contextOptions.Geolocation = new Geolocation {
                Latitude = (float)options.GeoLatitude.Value,
                Longitude = (float)options.GeoLongitude.Value,
                Accuracy = 0
            };
        }

        if (options.Permissions.Count > 0) {
            contextOptions.Permissions = options.Permissions;
        } else if (options.GeoLatitude.HasValue && options.GeoLongitude.HasValue) {
            contextOptions.Permissions = new[] { "geolocation" };
        }

        if (!string.IsNullOrWhiteSpace(options.Timezone)) {
            contextOptions.TimezoneId = options.Timezone;
        }
    }

    private static void StartBestEffortClose(Func<Task> close) {
        try {
            Task task = close();
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
        } catch (Exception) {
            // The transport is already gone; disposing Playwright below completes ownership cleanup.
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
                return await page.InnerTextAsync("html").WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return await page.ContentAsync().WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        var locator = page.Locator(selector!);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions {
            Timeout = timeout
        }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);

        if (asText) {
            return await locator.InnerTextAsync().WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }
        if (innerHtml) {
            return await locator.InnerHTMLAsync().WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }
        return await locator.EvaluateAsync<string>("el => el.outerHTML").WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
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
