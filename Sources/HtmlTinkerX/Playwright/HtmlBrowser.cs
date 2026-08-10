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
    internal static Func<Task<IPlaywright>>? PlaywrightFactory { get; set; }

    internal static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchBrowserAsync(
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Clean) {
            await CleanInstallationAsync().ConfigureAwait(false);
        }

        await EnsureBrowserRuntimeAvailableAsync(options).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        IPlaywright playwright = PlaywrightFactory != null
            ? await PlaywrightFactory().ConfigureAwait(false)
            : await Playwright.CreateAsync();
        int playwrightDisposed = 0;
        Task DisposeOwnerAsync() {
            if (Interlocked.Exchange(ref playwrightDisposed, 1) == 0) playwright.Dispose();
            return Task.CompletedTask;
        }
        try {
            IBrowserType type = ResolveBrowserType(playwright, options.Browser);
            BrowserTypeLaunchOptions launchOptions = CreateLaunchOptions(options);

            IBrowser browserInstance = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => type.LaunchAsync(launchOptions),
                DisposeOwnerAsync,
                cancellationToken).ConfigureAwait(false);
            return (playwright, browserInstance);
        } catch {
            await DisposeOwnerAsync().ConfigureAwait(false);
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
            await EnsureDriverInstalledAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IPlaywright playwright = PlaywrightFactory != null
            ? await PlaywrightFactory().ConfigureAwait(false)
            : await Playwright.CreateAsync();
        int playwrightDisposed = 0;
        Task DisposeOwnerAsync() {
            if (Interlocked.Exchange(ref playwrightDisposed, 1) == 0) playwright.Dispose();
            return Task.CompletedTask;
        }
        try {
            IBrowser browserInstance = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => playwright.Chromium.ConnectOverCDPAsync(
                    options.CdpEndpointUrl!,
                    new BrowserTypeConnectOverCDPOptions { Timeout = options.Timeout }),
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
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Clean) {
            await CleanInstallationAsync().ConfigureAwait(false);
        }

        await EnsureBrowserRuntimeAvailableAsync(options).ConfigureAwait(false);

        string userDataDirectory = HtmlUtilities.EnsureDirectoryExists(options.UserDataDirectory!);

        cancellationToken.ThrowIfCancellationRequested();
        IPlaywright playwright = PlaywrightFactory != null
            ? await PlaywrightFactory().ConfigureAwait(false)
            : await Playwright.CreateAsync();
        int playwrightDisposed = 0;
        Task DisposeOwnerAsync() {
            if (Interlocked.Exchange(ref playwrightDisposed, 1) == 0) playwright.Dispose();
            return Task.CompletedTask;
        }
        IBrowserContext? context = null;
        try {
            IBrowserType type = ResolveBrowserType(playwright, options.Browser);
            BrowserTypeLaunchPersistentContextOptions contextOptions = CreatePersistentContextOptions(options);

            context = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => type.LaunchPersistentContextAsync(userDataDirectory, contextOptions),
                DisposeOwnerAsync,
                cancellationToken).ConfigureAwait(false);
            IPage page = context.Pages.Count > 0
                ? context.Pages[0]
                : await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    context.NewPageAsync,
                    () => context.CloseAsync(),
                    cancellationToken).ConfigureAwait(false);

            return (playwright, context, page);
        } catch {
            if (context != null) {
                StartBestEffortClose(() => context.CloseAsync());
            }
            await DisposeOwnerAsync().ConfigureAwait(false);
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

    private static BrowserTypeLaunchOptions CreateLaunchOptions(HtmlBrowserLaunchOptions options) {
        var launchOptions = new BrowserTypeLaunchOptions {
            Headless = options.Headless,
            SlowMo = options.SlowMo
        };

        if (!string.IsNullOrEmpty(options.BrowserChannel)) {
            launchOptions.Channel = options.BrowserChannel;
        }

        if (!string.IsNullOrEmpty(options.BrowserExecutablePath)) {
            launchOptions.ExecutablePath = options.BrowserExecutablePath!.ToFullPath();
        }

        if (options.BrowserArguments.Count > 0) {
            launchOptions.Args = options.BrowserArguments;
        }

        if (options.ChromiumSandbox.HasValue) {
            launchOptions.ChromiumSandbox = options.ChromiumSandbox.Value;
        }

        if (!string.IsNullOrEmpty(options.Proxy)) {
            launchOptions.Proxy = new Proxy {
                Server = options.Proxy!,
                Username = options.ProxyUsername,
                Password = options.ProxyPassword
            };
        }

        return launchOptions;
    }

    private static BrowserTypeLaunchPersistentContextOptions CreatePersistentContextOptions(HtmlBrowserLaunchOptions options) {
        BrowserTypeLaunchPersistentContextOptions contextOptions = new() {
            Headless = options.Headless,
            SlowMo = options.SlowMo,
            IgnoreHTTPSErrors = options.IgnoreHTTPSErrors
        };

        ApplySharedContextOptions(
            contextOptions,
            options,
            setStorageState: false);

        if (options.FormLogin == null && !string.IsNullOrEmpty(options.Username) && options.Password != null) {
            contextOptions.HttpCredentials = new HttpCredentials {
                Username = options.Username!,
                Password = options.Password!
            };
        }

        if (!string.IsNullOrEmpty(options.BrowserChannel)) {
            contextOptions.Channel = options.BrowserChannel;
        }

        if (!string.IsNullOrEmpty(options.BrowserExecutablePath)) {
            contextOptions.ExecutablePath = options.BrowserExecutablePath!.ToFullPath();
        }

        if (options.BrowserArguments.Count > 0) {
            contextOptions.Args = options.BrowserArguments;
        }

        if (options.ChromiumSandbox.HasValue) {
            contextOptions.ChromiumSandbox = options.ChromiumSandbox.Value;
        }

        if (!string.IsNullOrEmpty(options.Proxy)) {
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
        bool closeBrowserOnCancellation = true) {
        BrowserNewContextOptions? contextOptions = null;
        if (options.FormLogin == null && !string.IsNullOrEmpty(options.Username) && options.Password != null) {
            contextOptions = new BrowserNewContextOptions {
                HttpCredentials = new HttpCredentials {
                    Username = options.Username!,
                    Password = options.Password!
                }
            };
        }

        contextOptions ??= new BrowserNewContextOptions();
        contextOptions.IgnoreHTTPSErrors = options.IgnoreHTTPSErrors;

        ApplySharedContextOptions(contextOptions, options, setStorageState: true);

        Task<IBrowserContext> contextCreation = browserInstance.NewContextAsync(contextOptions);
        IBrowserContext context;
        try {
            context = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => contextCreation,
                closeBrowserOnCancellation ? () => browserInstance.CloseAsync() : static () => Task.CompletedTask,
                cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (!closeBrowserOnCancellation) {
            CloseContextWhenCreated(contextCreation);
            throw;
        }
        try {
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

    private static void CloseContextWhenCreated(Task<IBrowserContext> contextCreation) {
        _ = contextCreation.ContinueWith(static completed => {
            if (completed.Status == TaskStatus.RanToCompletion) {
                StartBestEffortClose(() => completed.Result.CloseAsync());
            } else {
                _ = completed.Exception;
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static void ApplySharedContextOptions(BrowserNewContextOptions contextOptions, HtmlBrowserLaunchOptions options, bool setStorageState) {
        if (setStorageState && !string.IsNullOrEmpty(options.StorageStatePath)) {
            contextOptions.StorageStatePath = options.StorageStatePath;
        }

        if (!string.IsNullOrEmpty(options.VideoPath)) {
            string resolved = HtmlUtilities.EnsureDirectoryExists(options.VideoPath!);
            string dir = Path.GetDirectoryName(resolved) ?? resolved;
            contextOptions.RecordVideoDir = dir;
            contextOptions.RecordVideoSize = new RecordVideoSize { Width = options.VideoWidth, Height = options.VideoHeight };
        }

        if (!string.IsNullOrEmpty(options.UserAgent)) {
            contextOptions.UserAgent = options.UserAgent;
        }

        if (!string.IsNullOrEmpty(options.Locale)) {
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

        if (!string.IsNullOrEmpty(options.Timezone)) {
            contextOptions.TimezoneId = options.Timezone;
        }
    }

    private static void ApplySharedContextOptions(BrowserTypeLaunchPersistentContextOptions contextOptions, HtmlBrowserLaunchOptions options, bool setStorageState) {
        if (!string.IsNullOrEmpty(options.VideoPath)) {
            string resolved = HtmlUtilities.EnsureDirectoryExists(options.VideoPath!);
            string dir = Path.GetDirectoryName(resolved) ?? resolved;
            contextOptions.RecordVideoDir = dir;
            contextOptions.RecordVideoSize = new RecordVideoSize { Width = options.VideoWidth, Height = options.VideoHeight };
        }

        if (!string.IsNullOrEmpty(options.UserAgent)) {
            contextOptions.UserAgent = options.UserAgent;
        }

        if (!string.IsNullOrEmpty(options.Locale)) {
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

        if (!string.IsNullOrEmpty(options.Timezone)) {
            contextOptions.TimezoneId = options.Timezone;
        }
    }

    /// <summary>
    /// Creates a new Playwright browser session and navigates to the specified URL.
    /// </summary>
    private static async Task<HtmlBrowserSession> CreatePageAsync(
        string url,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken = default) {
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
        string? resolvedUserDataDirectory = null;
        bool closeContextOnDispose = true;
        bool closeBrowserOnDispose = true;
        bool closePageOnDispose = false;
        int ownershipAborted = 0;

        Task AbortOwnershipAsync() {
            if (Interlocked.Exchange(ref ownershipAborted, 1) != 0) return Task.CompletedTask;
            if (closePageOnDispose && page != null && !page.IsClosed) StartBestEffortClose(() => page.CloseAsync());
            if (closeContextOnDispose && context != null) StartBestEffortClose(() => context.CloseAsync());
            if (closeBrowserOnDispose && browserInstance != null && browserInstance.IsConnected) StartBestEffortClose(() => browserInstance.CloseAsync());
            try { playwright?.Dispose(); } catch (Exception) { }
            return Task.CompletedTask;
        }

        try {
            if (!string.IsNullOrWhiteSpace(options.CdpEndpointUrl)) {
                (playwright, browserInstance) = await ConnectOverCdpAsync(options, cancellationToken).ConfigureAwait(false);
                closeContextOnDispose = false;
                closeBrowserOnDispose = false;
                closePageOnDispose = true;
                if (browserInstance.Contexts.Count > 0) {
                    context = browserInstance.Contexts[0];
                    page = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                        context.NewPageAsync,
                        AbortOwnershipAsync,
                        cancellationToken).ConfigureAwait(false);
                } else {
                    closeContextOnDispose = true;
                    closePageOnDispose = false;
                    (context, page) = await CreateBrowserContextAsync(
                        browserInstance,
                        options,
                        cancellationToken,
                        closeBrowserOnCancellation: false).ConfigureAwait(false);
                }
            } else if (!string.IsNullOrWhiteSpace(options.UserDataDirectory)) {
                resolvedUserDataDirectory = HtmlUtilities.EnsureDirectoryExists(options.UserDataDirectory!);
                (playwright, context, page) = await LaunchPersistentContextAsync(options, cancellationToken).ConfigureAwait(false);
                browserInstance = context.Browser;
                closeBrowserOnDispose = false;
            } else {
                (playwright, browserInstance) = await LaunchBrowserAsync(options, cancellationToken).ConfigureAwait(false);
                (context, page) = await CreateBrowserContextAsync(browserInstance, options, cancellationToken).ConfigureAwait(false);
            }

            await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => ApplyInitScriptsAsync(context!, options, cancellationToken),
                AbortOwnershipAsync,
                cancellationToken).ConfigureAwait(false);

            var network = new System.Collections.Concurrent.ConcurrentDictionary<IRequest, HtmlNetworkEntry>();
            HtmlBrowserSession session = new(
                playwright!,
                browserInstance,
                context!,
                page!,
                !string.IsNullOrEmpty(options.VideoPath) ? page!.Video : null,
                options.VideoPath,
                network,
                resolvedUserDataDirectory,
                options.CdpEndpointUrl,
                closeContextOnDispose,
                closeBrowserOnDispose,
                closePageOnDispose);

            await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => ApplyResourceBlockingAsync(page!, options.BlockResourceTypes, options.BlockResourcePatterns, cancellationToken),
                AbortOwnershipAsync,
                cancellationToken).ConfigureAwait(false);
            await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                () => NavigateAsync(page!, url, options.FormLogin, options.Username, options.Password, options.LoadState, options.Timeout, cancellationToken),
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

    private static void StartBestEffortClose(Func<Task> close) {
        try {
            Task task = close();
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
        } catch (Exception) {
            // The transport is already gone; disposing Playwright below completes ownership cleanup.
        }
    }

    private static void ValidateCdpAttachOptions(HtmlBrowserLaunchOptions options) {
        if (!string.IsNullOrWhiteSpace(options.UserDataDirectory)) {
            throw new ArgumentException("CdpEndpointUrl attaches to an already-running browser. Launch Chrome with the desired --user-data-dir before attaching; do not combine CdpEndpointUrl with UserDataDirectory.");
        }

        if (!string.IsNullOrWhiteSpace(options.StorageStatePath)) {
            throw new ArgumentException("CdpEndpointUrl attaches to an existing browser context. Do not combine CdpEndpointUrl with StatePath or StorageStatePath.");
        }

        if (!string.IsNullOrWhiteSpace(options.BrowserChannel) || !string.IsNullOrWhiteSpace(options.BrowserExecutablePath) || options.Clean) {
            throw new ArgumentException("CdpEndpointUrl attaches to an already-running browser, so BrowserChannel, BrowserExecutablePath, and Clean are not used.");
        }

        if (HasCdpContextOnlyOptions(options)) {
            throw new ArgumentException("CdpEndpointUrl attaches to an existing browser context, so context options such as Proxy, UserAgent, Locale, viewport, geolocation, timezone, and permissions are not applied. Launch Chrome with those settings before attaching.");
        }
    }

    private static bool HasCdpContextOnlyOptions(HtmlBrowserLaunchOptions options) =>
        !string.IsNullOrWhiteSpace(options.Proxy)
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

    private static async Task ApplyInitScriptsAsync(IBrowserContext context, HtmlBrowserLaunchOptions options, CancellationToken cancellationToken) {
        if (options.PreventSsoAutoSubmit) {
            cancellationToken.ThrowIfCancellationRequested();
            await context.AddInitScriptAsync(PreventSsoAutoSubmitInitScript).ConfigureAwait(false);
        }

        foreach (string script in options.InitScripts) {
            cancellationToken.ThrowIfCancellationRequested();
            await context.AddInitScriptAsync(script).ConfigureAwait(false);
        }

        foreach (string scriptPath in options.InitScriptPaths) {
            cancellationToken.ThrowIfCancellationRequested();
            await context.AddInitScriptAsync(scriptPath: scriptPath.ToFullPath()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> using reusable launch options and navigates to the specified URL.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="options">Browser launch, profile, emulation, and network options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<HtmlBrowserSession> OpenSessionAsync(
        string url,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken = default) {
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }

        return CreatePageAsync(url, options, cancellationToken);
    }

    /// <summary>
    /// Creates a new <see cref="HtmlBrowserSession"/> using reusable launch options and navigates to the specified URL.
    /// </summary>
    /// <param name="url">URL to navigate to.</param>
    /// <param name="options">Browser launch, profile, emulation, and network options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<HtmlBrowserSession> OpenSessionWithOptionsAsync(
        string url,
        HtmlBrowserLaunchOptions options,
        CancellationToken cancellationToken = default)
        => OpenSessionAsync(url, options, cancellationToken);

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
        => OpenSessionAsync(
            url,
            HtmlBrowserLaunchOptions.FromLegacyParameters(
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
                blockResourceTypes,
                blockResourcePatterns,
                loadState,
                timeout),
            cancellationToken);

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
